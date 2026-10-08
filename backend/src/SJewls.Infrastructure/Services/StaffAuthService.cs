using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SJewls.Application.Common;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class StaffAuthService : IStaffAuthService
{
    private readonly SJewlsDbContext _db;
    private readonly IPasswordHasher<Staff> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IAuditLogService _auditService;
    private readonly IConfiguration _config;
    private readonly ILogger<StaffAuthService> _logger;

    public StaffAuthService(
        SJewlsDbContext db,
        IPasswordHasher<Staff> passwordHasher,
        ITokenService tokenService,
        IEmailSender emailSender,
        IAuditLogService auditService,
        IConfiguration config,
        ILogger<StaffAuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _auditService = auditService;
        _config = config;
        _logger = logger;
    }

    public async Task SeedInitialSuperAdminAsync(CancellationToken cancellationToken = default)
    {
        // 1. Ensure Roles exist
        var roles = await _db.Roles.ToListAsync(cancellationToken);
        var superAdminRole = roles.FirstOrDefault(r => r.RoleType == StaffRoleType.SuperAdmin || r.Name.Equals("Super Admin", StringComparison.OrdinalIgnoreCase));
        if (superAdminRole == null)
        {
            superAdminRole = new Role
            {
                Name = "Super Admin",
                RoleType = StaffRoleType.SuperAdmin,
                Description = "Full platform administrator with unrestricted access",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            _db.Roles.Add(superAdminRole);
        }

        var branchAdminRole = roles.FirstOrDefault(r => r.RoleType == StaffRoleType.BranchAdmin || r.Name.Equals("Branch Admin", StringComparison.OrdinalIgnoreCase));
        if (branchAdminRole == null)
        {
            branchAdminRole = new Role
            {
                Name = "Branch Admin",
                RoleType = StaffRoleType.BranchAdmin,
                Description = "Branch-level administrator with branch management access",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            _db.Roles.Add(branchAdminRole);
        }

        var staffRole = roles.FirstOrDefault(r => r.RoleType == StaffRoleType.Staff || r.Name.Equals("Staff", StringComparison.OrdinalIgnoreCase));
        if (staffRole == null)
        {
            staffRole = new Role
            {
                Name = "Staff",
                RoleType = StaffRoleType.Staff,
                Description = "Standard branch operational staff member",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            _db.Roles.Add(staffRole);
        }

        // 2. Ensure default branch exists
        var defaultBranch = await _db.Branches.FirstOrDefaultAsync(b => b.Code == "JAF-01", cancellationToken);
        if (defaultBranch == null)
        {
            defaultBranch = new Branch
            {
                Code = "JAF-01",
                Name = "Jaffna Main Branch",
                Address = "124 Hospital Road, Jaffna",
                City = "Jaffna",
                Country = "Sri Lanka",
                Currency = "LKR",
                Timezone = "Asia/Colombo",
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            _db.Branches.Add(defaultBranch);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // 3. Seed Initial Super Admin from environment / config
        var initialUsername = _config["Admin:InitialUsername"] 
            ?? Environment.GetEnvironmentVariable("ADMIN_INITIAL_USERNAME");
        var initialEmail = _config["Admin:InitialEmail"] 
            ?? Environment.GetEnvironmentVariable("ADMIN_INITIAL_EMAIL");
        var initialPassword = _config["Admin:InitialPassword"] 
            ?? Environment.GetEnvironmentVariable("ADMIN_INITIAL_PASSWORD");
        var initialName = _config["Admin:InitialName"] 
            ?? Environment.GetEnvironmentVariable("ADMIN_INITIAL_NAME") 
            ?? "Super Administrator";
        var initialPhone = _config["Admin:InitialPhone"] 
            ?? Environment.GetEnvironmentVariable("ADMIN_INITIAL_PHONE") 
            ?? "+94770000000";

        if (string.IsNullOrWhiteSpace(initialEmail) || string.IsNullOrWhiteSpace(initialPassword))
        {
            _logger.LogInformation("No initial admin credentials configured. Skipping initial Super Admin seeding.");
            return;
        }

        var normalizedEmail = initialEmail.Trim().ToLowerInvariant();
        var normalizedUsername = !string.IsNullOrWhiteSpace(initialUsername) ? initialUsername.Trim() : null;

        var existingSuperAdmin = await _db.StaffMembers
            .Include(s => s.Roles).ThenInclude(r => r.Role)
            .Include(s => s.AssignedBranches)
            .FirstOrDefaultAsync(s => s.Email.ToLower() == normalizedEmail ||
                                      (normalizedUsername != null && s.Username != null && s.Username.ToLower() == normalizedUsername.ToLower()),
                                 cancellationToken);

        if (existingSuperAdmin != null)
        {
            var verify = string.IsNullOrWhiteSpace(existingSuperAdmin.PasswordHash) 
                ? PasswordVerificationResult.Failed 
                : _passwordHasher.VerifyHashedPassword(existingSuperAdmin, existingSuperAdmin.PasswordHash, initialPassword.Trim());

            bool updated = false;
            if (verify == PasswordVerificationResult.Failed)
            {
                existingSuperAdmin.PasswordHash = _passwordHasher.HashPassword(existingSuperAdmin, initialPassword.Trim());
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(existingSuperAdmin.Username) && normalizedUsername != null)
            {
                existingSuperAdmin.Username = normalizedUsername;
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(existingSuperAdmin.PhoneNumber))
            {
                existingSuperAdmin.PhoneNumber = initialPhone;
                updated = true;
            }

            if (!existingSuperAdmin.IsActive)
            {
                existingSuperAdmin.IsActive = true;
                updated = true;
            }

            if (!existingSuperAdmin.Roles.Any(r => r.Role != null && (r.Role.Name == "Super Admin" || r.Role.RoleType == StaffRoleType.SuperAdmin)))
            {
                _db.StaffRoles.Add(new StaffRole
                {
                    StaffId = existingSuperAdmin.Id,
                    RoleId = superAdminRole.Id,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
                updated = true;
            }

            if (!existingSuperAdmin.AssignedBranches.Any(b => b.BranchId == defaultBranch.Id))
            {
                _db.StaffBranches.Add(new StaffBranch
                {
                    StaffId = existingSuperAdmin.Id,
                    BranchId = defaultBranch.Id,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
                updated = true;
            }

            if (updated)
            {
                existingSuperAdmin.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Super Admin account ({Email}) synchronized with initial credentials and roles.", MaskEmail(normalizedEmail));
            }
            else
            {
                _logger.LogInformation("Super Admin account ({Email}) verified and ready.", MaskEmail(normalizedEmail));
            }
            return;
        }

        var superAdmin = new Staff
        {
            FullName = initialName,
            Email = normalizedEmail,
            Username = normalizedUsername,
            PhoneNumber = initialPhone,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        superAdmin.PasswordHash = _passwordHasher.HashPassword(superAdmin, initialPassword.Trim());
        _db.StaffMembers.Add(superAdmin);

        _db.StaffRoles.Add(new StaffRole
        {
            StaffId = superAdmin.Id,
            RoleId = superAdminRole.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        _db.StaffBranches.Add(new StaffBranch
        {
            StaffId = superAdmin.Id,
            BranchId = defaultBranch.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorType: "System",
            actorId: superAdmin.Id,
            action: "INITIAL_SUPER_ADMIN_SEEDED",
            targetEntity: "Staff",
            targetId: superAdmin.Id.ToString(),
            branchId: defaultBranch.Id,
            before: null,
            after: new { Email = normalizedEmail, Username = normalizedUsername, Role = "Super Admin" },
            ipAddress: "127.0.0.1");

        _logger.LogInformation("Initial Super Admin user seeded successfully for email {Email} (Username: {Username}).",
            MaskEmail(normalizedEmail), normalizedUsername ?? "N/A");
    }

    public async Task<StaffLoginResponse> LoginAsync(StaffLoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Username/email and password are required.");
        }

        var identifier = request.UsernameOrEmail.Trim().ToLowerInvariant();

        var staff = await _db.StaffMembers
            .Include(s => s.Roles).ThenInclude(r => r.Role)
            .Include(s => s.AssignedBranches).ThenInclude(b => b.Branch)
            .FirstOrDefaultAsync(s => s.Email.ToLower() == identifier ||
                                      (s.Username != null && s.Username.ToLower() == identifier),
                                 cancellationToken);

        if (staff == null || !staff.IsActive)
        {
            await _auditService.LogActionAsync(
                actorType: "Staff",
                actorId: null,
                action: "STAFF_LOGIN_FAILED",
                targetEntity: "Staff",
                targetId: identifier,
                branchId: null,
                before: null,
                after: new { Reason = "User not found or inactive", Identifier = MaskIdentifier(identifier) },
                ipAddress: ipAddress);

            throw new UnauthorizedAccessException("Invalid credentials or account is inactive.");
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(staff, staff.PasswordHash, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            await _auditService.LogActionAsync(
                actorType: "Staff",
                actorId: staff.Id,
                action: "STAFF_LOGIN_FAILED",
                targetEntity: "Staff",
                targetId: staff.Id.ToString(),
                branchId: staff.AssignedBranches.FirstOrDefault()?.BranchId,
                before: null,
                after: new { Reason = "Password verification failed" },
                ipAddress: ipAddress);

            throw new UnauthorizedAccessException("Invalid credentials or account is inactive.");
        }

        if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            staff.PasswordHash = _passwordHasher.HashPassword(staff, request.Password);
            staff.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var roles = staff.Roles
            .Where(r => r.Role != null)
            .Select(r => r.Role!.Name)
            .ToList();

        var branchCodes = staff.AssignedBranches
            .Where(b => b.Branch != null)
            .Select(b => b.Branch!.Code)
            .Distinct()
            .ToList();

        var accessToken = _tokenService.GenerateStaffAccessToken(staff, roles, branchCodes);
        var expiryMinutes = int.TryParse(_config["Jwt:AdminExpiryMinutes"], out var exp) ? exp : (int.TryParse(_config["Jwt:ExpiryMinutes"], out var defaultExp) ? defaultExp : 480);
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        await _auditService.LogActionAsync(
            actorType: "Staff",
            actorId: staff.Id,
            action: "STAFF_LOGIN_SUCCESS",
            targetEntity: "Staff",
            targetId: staff.Id.ToString(),
            branchId: staff.AssignedBranches.FirstOrDefault()?.BranchId,
            before: null,
            after: new { Roles = roles, BranchCodes = branchCodes },
            ipAddress: ipAddress);

        return new StaffLoginResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresAtUtc = expiresAtUtc,
            User = MapToDto(staff)
        };
    }

    public async Task<StaffUserDto> GetCurrentStaffAsync(Guid staffId, string? securityStamp, CancellationToken cancellationToken = default)
    {
        var staff = await _db.StaffMembers
            .Include(s => s.Roles).ThenInclude(r => r.Role)
            .Include(s => s.AssignedBranches).ThenInclude(b => b.Branch)
            .FirstOrDefaultAsync(s => s.Id == staffId, cancellationToken);

        if (staff == null || !staff.IsActive)
        {
            throw new UnauthorizedAccessException("Staff account not found or is inactive.");
        }

        if (!string.IsNullOrEmpty(securityStamp) && !string.Equals(staff.SecurityStamp, securityStamp, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Session has expired or was invalidated. Please log in again.");
        }

        return MapToDto(staff);
    }

    public async Task LogoutAsync(Guid staffId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var staff = await _db.StaffMembers
            .Include(s => s.AssignedBranches)
            .FirstOrDefaultAsync(s => s.Id == staffId, cancellationToken);

        if (staff != null)
        {
            staff.SecurityStamp = Guid.NewGuid().ToString("N");
            staff.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            await _auditService.LogActionAsync(
                actorType: "Staff",
                actorId: staff.Id,
                action: "STAFF_LOGOUT",
                targetEntity: "Staff",
                targetId: staff.Id.ToString(),
                branchId: staff.AssignedBranches.FirstOrDefault()?.BranchId,
                before: null,
                after: new { Status = "LoggedOut" },
                ipAddress: ipAddress);
        }
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, string? originUrl, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !ValidationHelper.IsValidEmail(request.Email))
        {
            return new ForgotPasswordResponse();
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var staff = await _db.StaffMembers
            .Include(s => s.AssignedBranches)
            .FirstOrDefaultAsync(s => s.Email.ToLower() == normalizedEmail, cancellationToken);

        if (staff != null && staff.IsActive)
        {
            var plainToken = GenerateSecureToken();
            staff.PasswordResetTokenHash = HashToken(plainToken);
            staff.PasswordResetExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(60);
            staff.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            var portalUrl = !string.IsNullOrWhiteSpace(originUrl)
                ? originUrl.TrimEnd('/')
                : (_config["Admin:PortalUrl"] ?? "http://localhost:3000").TrimEnd('/');

            var resetLink = $"{portalUrl}/reset-password?token={plainToken}&email={Uri.EscapeDataString(staff.Email)}";

            try
            {
                var emailSubject = "SJewls Admin Portal - Password Reset Instructions";
                var emailBody = $@"Hello {staff.FullName},

We received a request to reset your password for the SJewls Admin Portal.

To reset your password, please click the following secure link:
{resetLink}

This link is single-use and will expire in 60 minutes.

If you did not request a password reset, please ignore this email or contact your Super Administrator immediately.

SJewls Admin Security
Jaffna, Sri Lanka";

                await _emailSender.SendEmailAsync(staff.Email, emailSubject, emailBody, cancellationToken);

                await _auditService.LogActionAsync(
                    actorType: "Staff",
                    actorId: staff.Id,
                    action: "PASSWORD_RESET_REQUESTED",
                    targetEntity: "Staff",
                    targetId: staff.Id.ToString(),
                    branchId: staff.AssignedBranches.FirstOrDefault()?.BranchId,
                    before: null,
                    after: new { Email = MaskEmail(staff.Email) },
                    ipAddress: ipAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", MaskEmail(staff.Email));
            }
        }

        return new ForgotPasswordResponse();
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.NewPassword, request.ConfirmPassword))
        {
            throw new ArgumentException("New password and confirm password do not match.");
        }

        if (!ValidationHelper.IsValidPassword(request.NewPassword, out var passwordError))
        {
            throw new ArgumentException(passwordError);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var staff = await _db.StaffMembers
            .Include(s => s.AssignedBranches)
            .FirstOrDefaultAsync(s => s.Email.ToLower() == normalizedEmail, cancellationToken);

        if (staff == null || !staff.IsActive ||
            string.IsNullOrWhiteSpace(staff.PasswordResetTokenHash) ||
            staff.PasswordResetExpiresAtUtc == null)
        {
            throw new ArgumentException("Invalid or expired password reset token.");
        }

        if (staff.PasswordResetExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            staff.PasswordResetTokenHash = null;
            staff.PasswordResetExpiresAtUtc = null;
            await _db.SaveChangesAsync(cancellationToken);
            throw new ArgumentException("Password reset token has expired. Please request a new link.");
        }

        var computedHash = HashToken(request.Token.Trim());
        var computedBytes = Encoding.UTF8.GetBytes(computedHash);
        var storedBytes = Encoding.UTF8.GetBytes(staff.PasswordResetTokenHash.Trim().ToLowerInvariant());

        if (!CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes))
        {
            throw new ArgumentException("Invalid or expired password reset token.");
        }

        // Token is valid! Update password hash, invalidate token, and regenerate security stamp
        staff.PasswordHash = _passwordHasher.HashPassword(staff, request.NewPassword);
        staff.PasswordResetTokenHash = null;
        staff.PasswordResetExpiresAtUtc = null;
        staff.SecurityStamp = Guid.NewGuid().ToString("N"); // Invalidates all existing sessions!
        staff.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorType: "Staff",
            actorId: staff.Id,
            action: "PASSWORD_RESET_COMPLETED",
            targetEntity: "Staff",
            targetId: staff.Id.ToString(),
            branchId: staff.AssignedBranches.FirstOrDefault()?.BranchId,
            before: null,
            after: new { Status = "PasswordUpdated" },
            ipAddress: ipAddress);

        return new ResetPasswordResponse();
    }

    public async Task<StaffUserDto> CreateStaffUserAsync(CreateStaffUserRequest request, Guid currentStaffId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        // 1. Verify caller is Super Admin
        var caller = await _db.StaffMembers
            .Include(s => s.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(s => s.Id == currentStaffId, cancellationToken);

        if (caller == null || !caller.IsActive || !caller.Roles.Any(r => r.Role?.Name == "Super Admin" || r.Role?.RoleType == StaffRoleType.SuperAdmin))
        {
            throw new UnauthorizedAccessException("Only Super Admin can create staff accounts.");
        }

        // 2. Validate fields
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2)
        {
            throw new ArgumentException("Full name must be at least 2 characters long.");
        }

        if (!ValidationHelper.IsValidEmail(request.Email))
        {
            throw new ArgumentException("Invalid email address format.");
        }

        if (!ValidationHelper.TryNormalizePhone(request.PhoneNumber, out var normalizedPhone))
        {
            throw new ArgumentException("Invalid phone number format.");
        }

        if (!ValidationHelper.IsValidPassword(request.Password, out var passError))
        {
            throw new ArgumentException(passError);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 3. Check duplicate email
        var emailExists = await _db.StaffMembers.AnyAsync(s => s.Email.ToLower() == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException("A staff account with this email address already exists.");
        }

        // 4. Check duplicate username if provided
        string? normalizedUsername = null;
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            normalizedUsername = request.Username.Trim();
            var usernameLower = normalizedUsername.ToLowerInvariant();
            var usernameExists = await _db.StaffMembers.AnyAsync(s => s.Username != null && s.Username.ToLower() == usernameLower, cancellationToken);
            if (usernameExists)
            {
                throw new InvalidOperationException("A staff account with this username already exists.");
            }
        }

        // 5. Lookup Role
        var roleEntity = await _db.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == request.Role.Trim().ToLower(), cancellationToken);
        if (roleEntity == null)
        {
            throw new ArgumentException($"Role '{request.Role}' not found.");
        }

        // 6. Branch check
        Branch? branch = null;
        if (request.BranchId.HasValue)
        {
            branch = await _db.Branches.FindAsync(new object[] { request.BranchId.Value }, cancellationToken);
            if (branch == null)
            {
                throw new ArgumentException("Specified branch not found.");
            }
        }
        else if (roleEntity.RoleType == StaffRoleType.BranchAdmin || roleEntity.RoleType == StaffRoleType.Staff)
        {
            // Default to JAF-01 if none specified
            branch = await _db.Branches.FirstOrDefaultAsync(b => b.Code == "JAF-01", cancellationToken);
        }

        // 7. Create Staff Member
        var newStaff = new Staff
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            Username = normalizedUsername,
            PhoneNumber = normalizedPhone,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        newStaff.PasswordHash = _passwordHasher.HashPassword(newStaff, request.Password);
        _db.StaffMembers.Add(newStaff);

        _db.StaffRoles.Add(new StaffRole
        {
            StaffId = newStaff.Id,
            RoleId = roleEntity.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        if (branch != null)
        {
            _db.StaffBranches.Add(new StaffBranch
            {
                StaffId = newStaff.Id,
                BranchId = branch.Id,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorType: "Staff",
            actorId: currentStaffId,
            action: "STAFF_CREATED",
            targetEntity: "Staff",
            targetId: newStaff.Id.ToString(),
            branchId: branch?.Id,
            before: null,
            after: new { Email = normalizedEmail, Username = normalizedUsername, Role = roleEntity.Name, Branch = branch?.Code },
            ipAddress: ipAddress);

        return await GetCurrentStaffAsync(newStaff.Id, null, cancellationToken);
    }

    public async Task<List<StaffUserDto>> GetStaffUsersAsync(Guid currentStaffId, CancellationToken cancellationToken = default)
    {
        var caller = await _db.StaffMembers
            .Include(s => s.Roles).ThenInclude(r => r.Role)
            .Include(s => s.AssignedBranches)
            .FirstOrDefaultAsync(s => s.Id == currentStaffId, cancellationToken);

        if (caller == null || !caller.IsActive)
        {
            throw new UnauthorizedAccessException("Unauthorized access.");
        }

        var isSuperAdmin = caller.Roles.Any(r => r.Role?.Name == "Super Admin" || r.Role?.RoleType == StaffRoleType.SuperAdmin);
        var isBranchAdmin = caller.Roles.Any(r => r.Role?.Name == "Branch Admin" || r.Role?.RoleType == StaffRoleType.BranchAdmin);

        if (!isSuperAdmin && !isBranchAdmin)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to view staff users.");
        }

        IQueryable<Staff> query = _db.StaffMembers
            .Include(s => s.Roles).ThenInclude(r => r.Role)
            .Include(s => s.AssignedBranches).ThenInclude(b => b.Branch);

        if (!isSuperAdmin && isBranchAdmin)
        {
            var branchIds = caller.AssignedBranches.Select(b => b.BranchId).ToList();
            query = query.Where(s => s.AssignedBranches.Any(ab => branchIds.Contains(ab.BranchId)));
        }

        var staffList = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return staffList.Select(MapToDto).ToList();
    }

    public async Task<StaffUserDto> UpdateStaffStatusAsync(
        Guid currentStaffId,
        Guid targetStaffId,
        UpdateStaffStatusRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        // 1. Prevent staff from deactivating themselves
        if (targetStaffId == currentStaffId)
        {
            throw new InvalidOperationException("You cannot deactivate or change the status of your own staff account.");
        }

        // 2. Verify caller existence and permissions
        var caller = await _db.StaffMembers
            .Include(s => s.Roles).ThenInclude(r => r.Role)
            .Include(s => s.AssignedBranches)
            .FirstOrDefaultAsync(s => s.Id == currentStaffId, cancellationToken);

        if (caller == null || !caller.IsActive)
        {
            throw new UnauthorizedAccessException("Unauthorized staff access or account is inactive.");
        }

        var isCallerSuperAdmin = caller.Roles.Any(r => r.Role?.Name == "Super Admin" || r.Role?.RoleType == StaffRoleType.SuperAdmin);
        var isCallerBranchAdmin = caller.Roles.Any(r => r.Role?.Name == "Branch Admin" || r.Role?.RoleType == StaffRoleType.BranchAdmin);

        if (!isCallerSuperAdmin && !isCallerBranchAdmin)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to manage staff accounts.");
        }

        // 3. Retrieve target staff
        var targetStaff = await _db.StaffMembers
            .Include(s => s.Roles).ThenInclude(r => r.Role)
            .Include(s => s.AssignedBranches).ThenInclude(b => b.Branch)
            .FirstOrDefaultAsync(s => s.Id == targetStaffId, cancellationToken);

        if (targetStaff == null)
        {
            throw new KeyNotFoundException($"Staff member with ID {targetStaffId} was not found.");
        }

        var isTargetSuperAdmin = targetStaff.Roles.Any(r => r.Role?.Name == "Super Admin" || r.Role?.RoleType == StaffRoleType.SuperAdmin);

        // 4. Branch admin restrictions
        if (!isCallerSuperAdmin)
        {
            if (isTargetSuperAdmin)
            {
                throw new UnauthorizedAccessException("You do not have permission to modify a Super Administrator account.");
            }

            var callerBranchIds = caller.AssignedBranches.Select(b => b.BranchId).ToList();
            var targetBranchIds = targetStaff.AssignedBranches.Select(b => b.BranchId).ToList();
            if (!targetBranchIds.Any(tb => callerBranchIds.Contains(tb)))
            {
                throw new UnauthorizedAccessException("You can only manage staff accounts within your assigned branch.");
            }
        }

        // 5. Protect the last active Super Admin
        if (isTargetSuperAdmin && !request.IsActive)
        {
            var activeSuperAdmins = await _db.StaffMembers
                .Where(s => s.IsActive && s.Roles.Any(r => r.Role != null && (r.Role.Name == "Super Admin" || r.Role.RoleType == StaffRoleType.SuperAdmin)))
                .CountAsync(cancellationToken);

            if (activeSuperAdmins <= 1)
            {
                throw new InvalidOperationException("Cannot deactivate the last active Super Administrator.");
            }
        }

        // 6. Apply status update
        if (!request.IsActive)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new ArgumentException("A reason is required to deactivate a staff account.");
            }

            targetStaff.IsActive = false;
            targetStaff.DeactivatedAtUtc = DateTimeOffset.UtcNow;
            targetStaff.DeactivationReason = request.Reason.Trim();
            targetStaff.DeactivatedByStaffId = currentStaffId;

            // Invalidate all existing sessions and access tokens immediately
            targetStaff.SecurityStamp = Guid.NewGuid().ToString("N");

            await _auditService.LogActionAsync(
                actorType: "Staff",
                actorId: currentStaffId,
                action: "STAFF_DEACTIVATED",
                targetEntity: "Staff",
                targetId: targetStaff.Id.ToString(),
                branchId: targetStaff.AssignedBranches.FirstOrDefault()?.BranchId,
                before: new { IsActive = true },
                after: new { IsActive = false, Reason = targetStaff.DeactivationReason },
                ipAddress: ipAddress);
        }
        else
        {
            targetStaff.IsActive = true;
            targetStaff.DeactivatedAtUtc = null;
            targetStaff.DeactivationReason = null;
            targetStaff.DeactivatedByStaffId = null;

            await _auditService.LogActionAsync(
                actorType: "Staff",
                actorId: currentStaffId,
                action: "STAFF_ACTIVATED",
                targetEntity: "Staff",
                targetId: targetStaff.Id.ToString(),
                branchId: targetStaff.AssignedBranches.FirstOrDefault()?.BranchId,
                before: new { IsActive = false },
                after: new { IsActive = true, Reason = request.Reason },
                ipAddress: ipAddress);
        }

        targetStaff.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(targetStaff);
    }

    private static StaffUserDto MapToDto(Staff staff)
    {
        return new StaffUserDto
        {
            Id = staff.Id,
            FullName = staff.FullName,
            Email = staff.Email,
            Username = staff.Username,
            PhoneNumber = staff.PhoneNumber,
            IsActive = staff.IsActive,
            Roles = staff.Roles
                .Where(r => r.Role != null)
                .Select(r => r.Role!.Name)
                .ToList(),
            AssignedBranches = staff.AssignedBranches
                .Where(b => b.Branch != null)
                .GroupBy(b => b.BranchId)
                .Select(g => g.First())
                .Select(b => new StaffBranchDto
                {
                    BranchId = b.Branch!.Id,
                    Code = b.Branch.Code,
                    Name = b.Branch.Name,
                    City = b.Branch.City
                })
                .ToList(),
            CreatedAtUtc = staff.CreatedAtUtc,
            UpdatedAtUtc = staff.UpdatedAtUtc,
            DeactivatedAtUtc = staff.DeactivatedAtUtc,
            DeactivationReason = staff.DeactivationReason
        };
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return "***";
        var parts = email.Split('@');
        var name = parts[0];
        var masked = name.Length <= 2 ? name : $"{name[0]}***{name[^1]}";
        return $"{masked}@{parts[1]}";
    }

    private static string MaskIdentifier(string identifier)
    {
        if (identifier.Contains('@'))
            return MaskEmail(identifier);
        return identifier.Length <= 3 ? "***" : $"{identifier[..2]}***";
    }
}
