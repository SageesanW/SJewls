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

    public async Task<VerifyOtpResponse> ProcessOtpVerificationAsync(string contact, string code, string? ipAddress)
    {
        var (success, message, normalizedContact, contactType, _) = await _otpService.VerifyOtpAsync(contact, code, "LoginOrRegister");

        if (!success)
        {
            throw new ArgumentException(message);
        }

        // Check if verified contact belongs to an existing customer
        var existingContact = await _db.CustomerContacts
            .Include(c => c.Customer)
                .ThenInclude(cust => cust!.PrimaryBranch)
            .FirstOrDefaultAsync(c => c.Type == contactType && c.Value == normalizedContact && c.IsVerified);

        var existingCustomer = existingContact?.Customer;

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

        // 5. Validate Additional Contact
        ContactType additionalType;
        string normalizedAdditional;

        if (session.ContactType == ContactType.Phone)
        {
            additionalType = ContactType.Email;
            if (!ValidationHelper.IsValidEmail(request.AdditionalContact))
            {
                throw new ArgumentException("A valid email address is required as your additional contact.");
            }
            normalizedAdditional = request.AdditionalContact.Trim().ToLowerInvariant();
        }
        else
        {
            additionalType = ContactType.Phone;
            if (!ValidationHelper.TryNormalizePhone(request.AdditionalContact, out normalizedAdditional))
            {
                throw new ArgumentException("A valid phone number (e.g., +947XXXXXXXX) is required as your additional contact.");
            }
        }

        // Ensure additional contact is not already verified by another customer
        var additionalExists = await _db.CustomerContacts
            .AnyAsync(c => c.Type == additionalType && c.Value == normalizedAdditional && c.IsVerified);

        if (additionalExists)
        {
            throw new ArgumentException("The provided additional contact is already verified by another customer account.");
        }

        // Ensure initial verified contact is not verified by another customer
        var initialExists = await _db.CustomerContacts
            .AnyAsync(c => c.Type == session.ContactType && c.Value == session.ContactValue && c.IsVerified &&
                           (!session.CustomerId.HasValue || c.CustomerId != session.CustomerId.Value));

        if (initialExists)
        {
            throw new ArgumentException("Your initial contact is already verified by another customer account.");
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
                customer.PrimaryBranchId = jaffnaBranch.Id;
                customer.IsActive = true;
                customer.IsProfileComplete = true;

                // Add or update initial verified contact
                var existingInitialContact = await _db.CustomerContacts
                    .FirstOrDefaultAsync(c => c.CustomerId == customer.Id && c.Type == session.ContactType && c.Value == session.ContactValue);

                if (existingInitialContact == null)
                {
                    _db.CustomerContacts.Add(new CustomerContact
                    {
                        CustomerId = customer.Id,
                        Type = session.ContactType,
                        Value = session.ContactValue,
                        IsVerified = true,
                        VerifiedAtUtc = DateTimeOffset.UtcNow,
                        IsPrimary = true
                    });
                }
                else
                {
                    existingInitialContact.IsVerified = true;
                    existingInitialContact.VerifiedAtUtc = DateTimeOffset.UtcNow;
                    existingInitialContact.IsPrimary = true;
                }

                // Add additional unverified contact (must pass separate OTP verification before being usable for login)
                var existingAdditionalContact = await _db.CustomerContacts
                    .FirstOrDefaultAsync(c => c.CustomerId == customer.Id && c.Type == additionalType && c.Value == normalizedAdditional);

                if (existingAdditionalContact == null)
                {
                    _db.CustomerContacts.Add(new CustomerContact
                    {
                        CustomerId = customer.Id,
                        Type = additionalType,
                        Value = normalizedAdditional,
                        IsVerified = false,
                        VerifiedAtUtc = null,
                        IsPrimary = false
                    });
                }

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
                    after: new { customer.FullName, customer.Nic, customer.DateOfBirth, InitialContact = session.ContactValue, AdditionalContact = normalizedAdditional },
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
            .Include(c => c.Contacts)
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
            PrimaryBranchId = customer.PrimaryBranchId,
            PrimaryBranchCode = customer.PrimaryBranch?.Code ?? "JAF-01",
            PrimaryBranchName = customer.PrimaryBranch?.Name ?? "Jaffna Branch",
            IsProfileComplete = customer.IsProfileComplete,
            CreatedAtUtc = customer.CreatedAtUtc,
            Contacts = customer.Contacts.Select(c => new CustomerContactDto
            {
                Id = c.Id,
                Type = c.Type,
                Value = c.Value,
                IsVerified = c.IsVerified,
                VerifiedAtUtc = c.VerifiedAtUtc,
                IsPrimary = c.IsPrimary
            }).ToList()
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

        // Check if contact belongs to this customer
        var customerContact = await _db.CustomerContacts
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Type == contactType && c.Value == normalizedContact);

        if (customerContact == null)
        {
            throw new KeyNotFoundException("This contact does not exist on your profile. Please add it first.");
        }

        if (customerContact.IsVerified)
        {
            return new RequestOtpResponse
            {
                Success = false,
                Message = "This contact is already verified."
            };
        }

        // Verify no other customer has already verified this contact
        var isTaken = await _db.CustomerContacts
            .AnyAsync(c => c.Type == contactType && c.Value == normalizedContact && c.IsVerified && c.CustomerId != customerId);

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

        var customerContact = await _db.CustomerContacts
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Type == contactType && c.Value == normalizedContact);

        if (customerContact == null)
        {
            throw new KeyNotFoundException("Customer contact not found.");
        }

        customerContact.IsVerified = true;
        customerContact.VerifiedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        await _auditService.LogActionAsync(
            actorType: "Customer",
            actorId: customerId,
            action: "SECONDARY_CONTACT_VERIFIED",
            targetEntity: "CustomerContact",
            targetId: customerContact.Id.ToString(),
            branchId: null,
            before: new { IsVerified = false },
            after: new { IsVerified = true, customerContact.Value, customerContact.Type },
            ipAddress: null);

        return true;
    }
}
