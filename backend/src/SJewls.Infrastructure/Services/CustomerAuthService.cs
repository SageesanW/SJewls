using Microsoft.EntityFrameworkCore;
using SJewls.Application.Common;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class CustomerAuthService : ICustomerAuthService
{
    private readonly SJewlsDbContext _db;
    private readonly IOtpService _otpService;
    private readonly ITokenService _tokenService;
    private readonly IAuditLogService _auditService;

    private const int RefreshTokenExpiryDays = 30;

    public CustomerAuthService(
        SJewlsDbContext db,
        IOtpService otpService,
        ITokenService tokenService,
        IAuditLogService auditService)
    {
        _db = db;
        _otpService = otpService;
        _tokenService = tokenService;
        _auditService = auditService;
    }

    public async Task<CheckContactResponse> CheckContactAsync(string contact)
    {
        if (!ValidationHelper.TryNormalizeContact(contact, out var normalizedContact, out var contactType))
        {
            throw new ArgumentException("Invalid phone number or email address format.");
        }

        var existingCustomer = await _db.Customers
            .FirstOrDefaultAsync(c => (contactType == ContactType.Phone && c.PhoneNumber == normalizedContact) ||
                                      (contactType == ContactType.Email && c.Email == normalizedContact));

        var exists = existingCustomer != null && existingCustomer.IsActive;
        var isProfileComplete = exists && existingCustomer!.IsProfileComplete;
        var nextAction = (exists && isProfileComplete) ? "Login" : "Register";
        var message = (exists && isProfileComplete)
            ? "Customer account found. Please request and verify an OTP to log in."
            : "Customer account not found or registration incomplete. Please request an OTP to proceed with registration.";

        return new CheckContactResponse
        {
            Exists = exists,
            IsProfileComplete = isProfileComplete,
            NormalizedContact = normalizedContact,
            ContactType = contactType,
            NextAction = nextAction,
            Message = message
        };
    }

    public async Task<VerifyOtpResponse> ProcessOtpVerificationAsync(string contact, string code, string? ipAddress)
    {
        var (success, message, normalizedContact, contactType, _) = await _otpService.VerifyOtpAsync(contact, code, "LoginOrRegister");

        if (!success)
        {
            throw new ArgumentException(message);
        }

        // Check if verified contact belongs to an existing customer
        var existingCustomer = await _db.Customers
            .Include(c => c.PrimaryBranch)
            .FirstOrDefaultAsync(c => (contactType == ContactType.Phone && c.PhoneNumber == normalizedContact) ||
                                      (contactType == ContactType.Email && c.Email == normalizedContact));

        // If customer exists and profile is fully complete -> Log them in!
        if (existingCustomer != null && existingCustomer.IsActive && existingCustomer.IsProfileComplete)
        {
            var branchCode = existingCustomer.PrimaryBranch?.Code ?? "JAF-01";
            var accessToken = _tokenService.GenerateAccessToken(existingCustomer, branchCode);
            var refreshTokenString = _tokenService.GenerateRefreshToken();

            var refreshToken = new RefreshToken
            {
                CustomerId = existingCustomer.Id,
                Token = refreshTokenString,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiryDays),
                IsRevoked = false,
                CreatedByIp = ipAddress
            };

            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync();

            await _auditService.LogActionAsync(
                actorType: "Customer",
                actorId: existingCustomer.Id,
                action: "CUSTOMER_LOGIN_OTP",
                targetEntity: "Customer",
                targetId: existingCustomer.Id.ToString(),
                branchId: existingCustomer.PrimaryBranchId,
                before: null,
                after: new { Contact = normalizedContact, LoginTime = DateTimeOffset.UtcNow },
                ipAddress: ipAddress);

            return new VerifyOtpResponse
            {
                NextAction = "Dashboard",
                AccessToken = accessToken,
                RefreshToken = refreshTokenString,
                ExpiresInSeconds = 86400,
                Customer = new CustomerSummaryDto
                {
                    Id = existingCustomer.Id,
                    FullName = existingCustomer.FullName,
                    Nic = existingCustomer.Nic,
                    PhoneNumber = existingCustomer.PhoneNumber,
                    Email = existingCustomer.Email,
                    PrimaryContact = normalizedContact,
                    PrimaryBranchCode = branchCode,
                    IsProfileComplete = true
                }
            };
        }

        // Otherwise (new customer OR profile incomplete) -> Create temporary Registration Session
        var registrationToken = _tokenService.GenerateRegistrationToken();
        var session = new RegistrationSession
        {
            RegistrationToken = registrationToken,
            ContactValue = normalizedContact,
            ContactType = contactType,
            CustomerId = existingCustomer?.Id,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
            IsCompleted = false
        };

        _db.RegistrationSessions.Add(session);
        await _db.SaveChangesAsync();

        var requiredAdditionalType = contactType == ContactType.Phone ? "Email" : "Phone";

        return new VerifyOtpResponse
        {
            NextAction = "CompleteProfile",
            RegistrationToken = registrationToken,
            RegistrationTokenExpiresInSeconds = 3600,
            VerifiedContact = normalizedContact,
            VerifiedContactType = contactType,
            RequiredAdditionalContactType = requiredAdditionalType
        };
    }

    public async Task<VerifyOtpResponse> CompleteRegistrationAsync(CompleteRegistrationRequest request, string? ipAddress)
    {
        // 1. Validate Registration Session
        var session = await _db.RegistrationSessions
            .FirstOrDefaultAsync(s => s.RegistrationToken == request.RegistrationToken && !s.IsCompleted);

        if (session == null || session.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Registration session has expired or is invalid. Please verify your contact again.");
        }

        // 2. Validate Full Name
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2)
        {
            throw new ArgumentException("Full name must be at least 2 characters long.");
        }

        // 3. Validate Date of Birth (Must be at least 18 years old)
        if (!ValidationHelper.IsValidDateOfBirth(request.DateOfBirth, minAgeYears: 18))
        {
            throw new ArgumentException("Customer must be at least 18 years of age to register.");
        }

        // 4. Validate NIC
        if (!ValidationHelper.TryNormalizeNic(request.Nic, out var normalizedNic))
        {
            throw new ArgumentException("Invalid Sri Lankan NIC format. Expected 9 digits followed by V/X (e.g. 951234567V) or 12 digits (e.g. 199512345678).");
        }

        // Check NIC uniqueness
        var nicExists = await _db.Customers
            .AnyAsync(c => c.Nic == normalizedNic && (!session.CustomerId.HasValue || c.Id != session.CustomerId.Value));

        if (nicExists)
        {
            throw new ArgumentException("A customer account with this NIC is already registered. Duplicate NICs are not permitted.");
        }

        // 5. Resolve Contact Information:
        // The initially entered contact in session.ContactValue MUST always be preserved and saved on the customer.
        string? emailToSave = null;
        string? phoneToSave = null;

        if (session.ContactType == ContactType.Phone)
        {
            phoneToSave = session.ContactValue; // Preserved initially entered phone number

            // Check if email was provided during registration (either in Email or AdditionalContact)
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                if (!ValidationHelper.IsValidEmail(request.Email))
                {
                    throw new ArgumentException("The provided email address format is invalid.");
                }
                emailToSave = request.Email.Trim().ToLowerInvariant();
            }
            else if (!string.IsNullOrWhiteSpace(request.AdditionalContact))
            {
                var candidate = request.AdditionalContact.Trim();
                if (ValidationHelper.IsValidEmail(candidate))
                {
                    emailToSave = candidate.ToLowerInvariant();
                }
                else if (ValidationHelper.TryNormalizePhone(candidate, out var candidatePhone))
                {
                    // User re-entered same phone number
                    if (candidatePhone != session.ContactValue)
                    {
                        throw new ArgumentException($"Registration started with phone number {session.ContactValue}. To provide an email during registration, please enter a valid email address.");
                    }
                }
                else
                {
                    throw new ArgumentException("Invalid contact format. Please provide a valid email address.");
                }
            }
        }
        else // session.ContactType == ContactType.Email
        {
            emailToSave = session.ContactValue; // Preserved initially entered email

            // Check if phone was provided during registration (either in PhoneNumber or AdditionalContact)
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                if (!ValidationHelper.TryNormalizePhone(request.PhoneNumber, out var normPhone))
                {
                    throw new ArgumentException("The provided phone number format is invalid. Expected format: +947XXXXXXXX or 07XXXXXXXX.");
                }
                phoneToSave = normPhone;
            }
            else if (!string.IsNullOrWhiteSpace(request.AdditionalContact))
            {
                var candidate = request.AdditionalContact.Trim();
                if (ValidationHelper.TryNormalizePhone(candidate, out var normPhone))
                {
                    phoneToSave = normPhone;
                }
                else if (ValidationHelper.IsValidEmail(candidate))
                {
                    if (!string.Equals(candidate, session.ContactValue, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException($"Registration started with email {session.ContactValue}. To provide a phone number during registration, please enter a valid phone number.");
                    }
                }
                else
                {
                    throw new ArgumentException("Invalid contact format. Please provide a valid phone number (e.g. +947XXXXXXXX).");
                }
            }
        }

        // Check uniqueness of secondary contact if provided
        if (!string.IsNullOrWhiteSpace(emailToSave) && session.ContactType != ContactType.Email)
        {
            var emailExists = await _db.Customers
                .AnyAsync(c => c.Email == emailToSave && (!session.CustomerId.HasValue || c.Id != session.CustomerId.Value));
            if (emailExists)
            {
                throw new ArgumentException("The provided email address is already registered to another customer account.");
            }
        }

        if (!string.IsNullOrWhiteSpace(phoneToSave) && session.ContactType != ContactType.Phone)
        {
            var phoneExists = await _db.Customers
                .AnyAsync(c => c.PhoneNumber == phoneToSave && (!session.CustomerId.HasValue || c.Id != session.CustomerId.Value));
            if (phoneExists)
            {
                throw new ArgumentException("The provided phone number is already registered to another customer account.");
            }
        }

        // 6. Execute atomic registration inside database transaction
        var executionStrategy = _db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Find primary branch (Jaffna)
                var jaffnaBranch = await _db.Branches.FirstOrDefaultAsync(b => b.Code == "JAF-01")
                    ?? await _db.Branches.FirstAsync();

                Customer customer;
                if (session.CustomerId.HasValue)
                {
                    customer = await _db.Customers.FindAsync(session.CustomerId.Value)
                        ?? new Customer { Id = session.CustomerId.Value };
                }
                else
                {
                    customer = new Customer();
                    _db.Customers.Add(customer);
                }

                customer.FullName = request.FullName.Trim();
                customer.DateOfBirth = request.DateOfBirth;
                customer.Nic = normalizedNic;

                if (session.ContactType == ContactType.Phone)
                {
                    // The initially entered phone number MUST be saved as verified
                    customer.PhoneNumber = session.ContactValue;
                    customer.IsPhoneVerified = true;
                    customer.PhoneVerifiedAtUtc ??= DateTimeOffset.UtcNow;

                    // If email was given during registration, save it
                    if (!string.IsNullOrWhiteSpace(emailToSave))
                    {
                        customer.Email = emailToSave;
                        if (!customer.IsEmailVerified)
                        {
                            customer.IsEmailVerified = false;
                            customer.EmailVerifiedAtUtc = null;
                        }
                    }
                }
                else // session.ContactType == ContactType.Email
                {
                    // The initially entered email MUST be saved as verified
                    customer.Email = session.ContactValue;
                    customer.IsEmailVerified = true;
                    customer.EmailVerifiedAtUtc ??= DateTimeOffset.UtcNow;

                    // If phone was given during registration, save it
                    if (!string.IsNullOrWhiteSpace(phoneToSave))
                    {
                        customer.PhoneNumber = phoneToSave;
                        if (!customer.IsPhoneVerified)
                        {
                            customer.IsPhoneVerified = false;
                            customer.PhoneVerifiedAtUtc = null;
                        }
                    }
                }

                customer.PrimaryBranchId = jaffnaBranch.Id;
                customer.IsActive = true;
                customer.IsProfileComplete = true;

                // Mark registration session completed
                session.IsCompleted = true;

                // Issue tokens
                var accessToken = _tokenService.GenerateAccessToken(customer, jaffnaBranch.Code);
                var refreshTokenString = _tokenService.GenerateRefreshToken();

                var refreshToken = new RefreshToken
                {
                    CustomerId = customer.Id,
                    Token = refreshTokenString,
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiryDays),
                    IsRevoked = false,
                    CreatedByIp = ipAddress
                };
                _db.RefreshTokens.Add(refreshToken);

                await _db.SaveChangesAsync();

                await _auditService.LogActionAsync(
                    actorType: "Customer",
                    actorId: customer.Id,
                    action: "CUSTOMER_REGISTRATION_COMPLETED",
                    targetEntity: "Customer",
                    targetId: customer.Id.ToString(),
                    branchId: jaffnaBranch.Id,
                    before: null,
                    after: new { customer.FullName, customer.Nic, customer.DateOfBirth, InitialContact = session.ContactValue, customer.PhoneNumber, customer.Email },
                    ipAddress: ipAddress);

                await tx.CommitAsync();

                return new VerifyOtpResponse
                {
                    NextAction = "Dashboard",
                    AccessToken = accessToken,
                    RefreshToken = refreshTokenString,
                    ExpiresInSeconds = 86400,
                    Customer = new CustomerSummaryDto
                    {
                        Id = customer.Id,
                        FullName = customer.FullName,
                        Nic = customer.Nic,
                        PhoneNumber = customer.PhoneNumber,
                        Email = customer.Email,
                        PrimaryContact = session.ContactValue,
                        PrimaryBranchCode = jaffnaBranch.Code,
                        IsProfileComplete = true
                    }
                };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<RefreshTokenResponse> RefreshTokenAsync(string refreshTokenString, string? ipAddress)
    {
        var token = await _db.RefreshTokens
            .Include(r => r.Customer)
                .ThenInclude(c => c!.PrimaryBranch)
            .FirstOrDefaultAsync(r => r.Token == refreshTokenString);

        if (token == null || token.IsRevoked || token.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid, expired, or revoked.");
        }

        if (token.Customer == null || !token.Customer.IsActive)
        {
            throw new UnauthorizedAccessException("Customer account is inactive or disabled.");
        }

        // Revoke current token and issue new token pair (refresh token rotation)
        token.IsRevoked = true;
        token.RevokedAtUtc = DateTimeOffset.UtcNow;
        token.RevokedByIp = ipAddress;

        var newRefreshTokenString = _tokenService.GenerateRefreshToken();
        token.ReplacedByToken = newRefreshTokenString;

        var newRefreshToken = new RefreshToken
        {
            CustomerId = token.CustomerId,
            Token = newRefreshTokenString,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiryDays),
            IsRevoked = false,
            CreatedByIp = ipAddress
        };

        _db.RefreshTokens.Add(newRefreshToken);
        await _db.SaveChangesAsync();

        var branchCode = token.Customer.PrimaryBranch?.Code ?? "JAF-01";
        var newAccessToken = _tokenService.GenerateAccessToken(token.Customer, branchCode);

        return new RefreshTokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshTokenString,
            ExpiresInSeconds = 86400
        };
    }

    public async Task LogoutAsync(string refreshTokenString, string? ipAddress)
    {
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == refreshTokenString && !r.IsRevoked);

        if (token != null)
        {
            token.IsRevoked = true;
            token.RevokedAtUtc = DateTimeOffset.UtcNow;
            token.RevokedByIp = ipAddress;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<CustomerProfileDto> GetCustomerProfileAsync(Guid customerId)
    {
        var customer = await _db.Customers
            .Include(c => c.PrimaryBranch)
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null)
        {
            throw new KeyNotFoundException("Customer profile not found.");
        }

        return new CustomerProfileDto
        {
            Id = customer.Id,
            FullName = customer.FullName,
            DateOfBirth = customer.DateOfBirth,
            Nic = customer.Nic,
            PhoneNumber = customer.PhoneNumber,
            Email = customer.Email,
            IsPhoneVerified = customer.IsPhoneVerified,
            IsEmailVerified = customer.IsEmailVerified,
            PhoneVerifiedAtUtc = customer.PhoneVerifiedAtUtc,
            EmailVerifiedAtUtc = customer.EmailVerifiedAtUtc,
            PrimaryBranchId = customer.PrimaryBranchId,
            PrimaryBranchCode = customer.PrimaryBranch?.Code ?? "JAF-01",
            PrimaryBranchName = customer.PrimaryBranch?.Name ?? "Jaffna Branch",
            IsProfileComplete = customer.IsProfileComplete,
            CreatedAtUtc = customer.CreatedAtUtc
        };
    }

    public async Task<RequestOtpResponse> RequestSecondaryContactOtpAsync(Guid customerId, string contact)
    {
        if (!ValidationHelper.TryNormalizeContact(contact, out var normalizedContact, out var contactType))
        {
            return new RequestOtpResponse
            {
                Success = false,
                Message = "Invalid contact format."
            };
        }

        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null)
        {
            throw new KeyNotFoundException("Customer profile not found.");
        }

        bool isMatch = (contactType == ContactType.Phone && customer.PhoneNumber == normalizedContact) ||
                       (contactType == ContactType.Email && customer.Email == normalizedContact);

        if (!isMatch)
        {
            throw new KeyNotFoundException("This contact does not match your profile.");
        }

        bool isAlreadyVerified = (contactType == ContactType.Phone && customer.IsPhoneVerified) ||
                                 (contactType == ContactType.Email && customer.IsEmailVerified);

        if (isAlreadyVerified)
        {
            return new RequestOtpResponse
            {
                Success = false,
                Message = "This contact is already verified."
            };
        }

        // Verify no other customer has already verified this contact
        var isTaken = await _db.Customers
            .AnyAsync(c => c.Id != customerId &&
                ((contactType == ContactType.Phone && c.PhoneNumber == normalizedContact && c.IsPhoneVerified) ||
                 (contactType == ContactType.Email && c.Email == normalizedContact && c.IsEmailVerified)));

        if (isTaken)
        {
            throw new InvalidOperationException("This contact has already been verified by another customer account.");
        }

        return await _otpService.RequestOtpAsync(normalizedContact, purpose: "VerifySecondaryContact", customerId: customerId);
    }

    public async Task<bool> VerifySecondaryContactOtpAsync(Guid customerId, string contact, string code)
    {
        var (success, message, normalizedContact, contactType, _) = await _otpService.VerifyOtpAsync(
            contact, code, purpose: "VerifySecondaryContact", customerId: customerId);

        if (!success)
        {
            throw new ArgumentException(message);
        }

        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null)
        {
            throw new KeyNotFoundException("Customer profile not found.");
        }

        if (contactType == ContactType.Phone)
        {
            customer.PhoneNumber = normalizedContact;
            customer.IsPhoneVerified = true;
            customer.PhoneVerifiedAtUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            customer.Email = normalizedContact;
            customer.IsEmailVerified = true;
            customer.EmailVerifiedAtUtc = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync(
            actorType: "Customer",
            actorId: customerId,
            action: "SECONDARY_CONTACT_VERIFIED",
            targetEntity: "Customer",
            targetId: customerId.ToString(),
            branchId: customer.PrimaryBranchId,
            before: null,
            after: new { Contact = normalizedContact, Type = contactType },
            ipAddress: null);

        return true;
    }
}
