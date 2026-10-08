using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class FakeEmailSender : IEmailSender
{
    public List<(string ToEmail, string Subject, string Body)> SentEmails { get; } = new();

    public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        SentEmails.Add((toEmail, subject, body));
        return Task.CompletedTask;
    }
}

public class StaffAuthAndUserManagementTests
{
    private static (SJewlsDbContext db, StaffAuthService authService, FakeEmailSender fakeEmail, PasswordHasher<Staff> passwordHasher) CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new SJewlsDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:InitialUsername"] = "superadmin",
                ["Admin:InitialEmail"] = "admin@sjewls.lk",
                ["Admin:InitialPassword"] = "SuperAdmin@2026!",
                ["Admin:InitialName"] = "Initial Super Admin",
                ["Admin:InitialPhone"] = "+94771234567",
                ["Admin:PortalUrl"] = "http://localhost:3000",
                ["Jwt:Secret"] = "super-secret-key-that-must-be-at-least-32-characters-long-sjewls-dev",
                ["Jwt:AdminExpiryMinutes"] = "480"
            })
            .Build();

        var passwordHasher = new PasswordHasher<Staff>();
        var tokenService = new TokenService(config);
        var fakeEmail = new FakeEmailSender();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var authService = new StaffAuthService(
            db,
            passwordHasher,
            tokenService,
            fakeEmail,
            auditService,
            config,
            NullLogger<StaffAuthService>.Instance);

        return (db, authService, fakeEmail, passwordHasher);
    }

    [Fact]
    public async Task SeedInitialSuperAdmin_CreatesRolesBranchAndSuperAdmin()
    {
        var (db, authService, _, passwordHasher) = CreateTestContext();

        // 1. Run Seeder
        await authService.SeedInitialSuperAdminAsync();

        // 2. Verify roles exist
        var roles = await db.Roles.ToListAsync();
        Assert.Contains(roles, r => r.Name == "Super Admin");
        Assert.Contains(roles, r => r.Name == "Branch Admin");
        Assert.Contains(roles, r => r.Name == "Staff");

        // 3. Verify Super Admin user created
        var superAdmin = await db.StaffMembers
            .Include(s => s.Roles).ThenInclude(r => r.Role)
            .Include(s => s.AssignedBranches).ThenInclude(b => b.Branch)
            .FirstOrDefaultAsync(s => s.Email == "admin@sjewls.lk");

        Assert.NotNull(superAdmin);
        Assert.Equal("superadmin", superAdmin.Username);
        Assert.True(superAdmin.IsActive);
        Assert.Single(superAdmin.Roles);
        Assert.Equal("Super Admin", superAdmin.Roles.First().Role!.Name);

        // Verify password hash is valid
        var verify = passwordHasher.VerifyHashedPassword(superAdmin, superAdmin.PasswordHash, "SuperAdmin@2026!");
        Assert.Equal(PasswordVerificationResult.Success, verify);

        // 4. Running seeder again should be idempotent and not create duplicates
        await authService.SeedInitialSuperAdminAsync();
        var count = await db.StaffMembers.CountAsync(s => s.Email == "admin@sjewls.lk");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndStaffDto()
    {
        var (_, authService, _, _) = CreateTestContext();
        await authService.SeedInitialSuperAdminAsync();

        // Login by username
        var responseByUsername = await authService.LoginAsync(new StaffLoginRequest
        {
            UsernameOrEmail = "superadmin",
            Password = "SuperAdmin@2026!"
        }, "127.0.0.1");

        Assert.NotNull(responseByUsername.AccessToken);
        Assert.Equal("admin@sjewls.lk", responseByUsername.User.Email);
        Assert.Contains("Super Admin", responseByUsername.User.Roles);

        // Login by email
        var responseByEmail = await authService.LoginAsync(new StaffLoginRequest
        {
            UsernameOrEmail = "admin@sjewls.lk",
            Password = "SuperAdmin@2026!"
        }, "127.0.0.1");

        Assert.NotNull(responseByEmail.AccessToken);
        Assert.Equal("superadmin", responseByEmail.User.Username);
    }

    [Fact]
    public async Task Login_WithWrongPasswordOrNonExistent_ThrowsUnauthorized()
    {
        var (_, authService, _, _) = CreateTestContext();
        await authService.SeedInitialSuperAdminAsync();

        // Non-existent user
        var ex1 = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.LoginAsync(new StaffLoginRequest
            {
                UsernameOrEmail = "nonexistent@sjewls.lk",
                Password = "Password123!"
            }, "127.0.0.1"));
        Assert.Equal("Invalid credentials or account is inactive.", ex1.Message);

        // Wrong password
        var ex2 = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.LoginAsync(new StaffLoginRequest
            {
                UsernameOrEmail = "superadmin",
                Password = "WrongPassword999!"
            }, "127.0.0.1"));
        Assert.Equal("Invalid credentials or account is inactive.", ex2.Message);
    }

    [Fact]
    public async Task CreateStaffUser_BySuperAdmin_SucceedsAndEnforcesDuplicateChecks()
    {
        var (db, authService, _, _) = CreateTestContext();
        await authService.SeedInitialSuperAdminAsync();

        var superAdmin = await db.StaffMembers.FirstAsync(s => s.Email == "admin@sjewls.lk");
        var branch = await db.Branches.FirstAsync(b => b.Code == "JAF-01");

        // 1. Create Staff
        var request = new CreateStaffUserRequest
        {
            FullName = "Kavitha Raman",
            Email = "kavitha@sjewls.lk",
            PhoneNumber = "+94779876543",
            Username = "kavitha",
            Password = "StaffPassword@2026!",
            Role = "Staff",
            BranchId = branch.Id
        };

        var createdStaff = await authService.CreateStaffUserAsync(request, superAdmin.Id, "127.0.0.1");

        Assert.NotNull(createdStaff);
        Assert.Equal("kavitha@sjewls.lk", createdStaff.Email);
        Assert.Equal("kavitha", createdStaff.Username);
        Assert.Contains("Staff", createdStaff.Roles);
        Assert.Contains(createdStaff.AssignedBranches, b => b.Code == "JAF-01");

        // 2. Duplicate Email Check
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            authService.CreateStaffUserAsync(new CreateStaffUserRequest
            {
                FullName = "Another Staff",
                Email = "kavitha@sjewls.lk",
                PhoneNumber = "+94771112233",
                Password = "StaffPassword@2026!",
                Role = "Staff"
            }, superAdmin.Id, "127.0.0.1"));

        // 3. Duplicate Username Check
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            authService.CreateStaffUserAsync(new CreateStaffUserRequest
            {
                FullName = "Another Staff",
                Email = "other@sjewls.lk",
                PhoneNumber = "+94771112233",
                Username = "kavitha",
                Password = "StaffPassword@2026!",
                Role = "Staff"
            }, superAdmin.Id, "127.0.0.1"));

        // 4. Non-super admin cannot create staff
        var nonAdminStaff = await db.StaffMembers.FirstAsync(s => s.Email == "kavitha@sjewls.lk");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.CreateStaffUserAsync(new CreateStaffUserRequest
            {
                FullName = "Third Staff",
                Email = "third@sjewls.lk",
                PhoneNumber = "+94775556677",
                Password = "StaffPassword@2026!",
                Role = "Staff"
            }, nonAdminStaff.Id, "127.0.0.1"));
    }

    [Fact]
    public async Task ForgotPassword_AlwaysReturnsSameMessage_AndSendsEmailIfEligible()
    {
        var (_, authService, fakeEmail, _) = CreateTestContext();
        await authService.SeedInitialSuperAdminAsync();

        // 1. Non-existent email -> Returns constant message, no email sent
        var respNonExistent = await authService.ForgotPasswordAsync(new ForgotPasswordRequest
        {
            Email = "nobody@sjewls.lk"
        }, "http://localhost:3000", "127.0.0.1");

        Assert.Equal("If an account exists for this email, password reset instructions have been sent.", respNonExistent.Message);
        Assert.Empty(fakeEmail.SentEmails);

        // 2. Existing email -> Returns constant message, sends email with reset link
        var respExisting = await authService.ForgotPasswordAsync(new ForgotPasswordRequest
        {
            Email = "admin@sjewls.lk"
        }, "http://localhost:3000", "127.0.0.1");

        Assert.Equal("If an account exists for this email, password reset instructions have been sent.", respExisting.Message);
        Assert.Single(fakeEmail.SentEmails);
        Assert.Equal("admin@sjewls.lk", fakeEmail.SentEmails[0].ToEmail);
        Assert.Contains("/reset-password?token=", fakeEmail.SentEmails[0].Body);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_UpdatesPasswordAndInvalidatesTokenAndSessions()
    {
        var (db, authService, fakeEmail, passwordHasher) = CreateTestContext();
        await authService.SeedInitialSuperAdminAsync();

        var initialAdmin = await db.StaffMembers.FirstAsync(s => s.Email == "admin@sjewls.lk");
        var initialSecurityStamp = initialAdmin.SecurityStamp;

        // 1. Request reset
        await authService.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "admin@sjewls.lk" }, "http://localhost:3000", "127.0.0.1");

        var resetEmail = fakeEmail.SentEmails.Single();
        var tokenMarker = "token=";
        var tokenStart = resetEmail.Body.IndexOf(tokenMarker) + tokenMarker.Length;
        var tokenEnd = resetEmail.Body.IndexOf("&", tokenStart);
        var rawToken = resetEmail.Body[tokenStart..tokenEnd];

        // 2. Reset with new password
        var newPass = "BrandNewPassword@2026!";
        var resetResponse = await authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = "admin@sjewls.lk",
            Token = rawToken,
            NewPassword = newPass,
            ConfirmPassword = newPass
        }, "127.0.0.1");

        Assert.Equal("Password has been successfully reset. Please log in with your new password.", resetResponse.Message);

        // 3. Verify staff state in DB
        var updatedAdmin = await db.StaffMembers.FirstAsync(s => s.Email == "admin@sjewls.lk");
        Assert.Null(updatedAdmin.PasswordResetTokenHash);
        Assert.Null(updatedAdmin.PasswordResetExpiresAtUtc);
        Assert.NotEqual(initialSecurityStamp, updatedAdmin.SecurityStamp); // Sessions invalidated!

        // New password works
        var verifyNew = passwordHasher.VerifyHashedPassword(updatedAdmin, updatedAdmin.PasswordHash, newPass);
        Assert.Equal(PasswordVerificationResult.Success, verifyNew);

        // Old password does NOT work
        var verifyOld = passwordHasher.VerifyHashedPassword(updatedAdmin, updatedAdmin.PasswordHash, "SuperAdmin@2026!");
        Assert.Equal(PasswordVerificationResult.Failed, verifyOld);

        // 4. Token cannot be reused (single-use)
        await Assert.ThrowsAsync<ArgumentException>(() =>
            authService.ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = "admin@sjewls.lk",
                Token = rawToken,
                NewPassword = "AnotherPassword@2026!",
                ConfirmPassword = "AnotherPassword@2026!"
            }, "127.0.0.1"));
    }

    [Fact]
    public async Task StaffStatus_SelfDeactivation_ThrowsInvalidOperationException()
    {
        var (db, authService, _, _) = CreateTestContext();
        await authService.SeedInitialSuperAdminAsync();

        var superAdmin = await db.StaffMembers.FirstAsync(s => s.Email == "admin@sjewls.lk");

        var req = new UpdateStaffStatusRequest { IsActive = false, Reason = "Accidental self-deactivation" };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            authService.UpdateStaffStatusAsync(superAdmin.Id, superAdmin.Id, req, "127.0.0.1"));
    }

    [Fact]
    public async Task StaffStatus_ProtectLastActiveSuperAdmin_ThrowsInvalidOperationException()
    {
        var (db, authService, _, _) = CreateTestContext();
        await authService.SeedInitialSuperAdminAsync();

        var superAdmin = await db.StaffMembers.FirstAsync(s => s.Email == "admin@sjewls.lk");

        // Create a secondary branch admin to attempt deactivating the sole super admin
        var branchAdmin = new Staff
        {
            FullName = "Branch Admin",
            Email = "branch@sjewls.lk",
            PhoneNumber = "+94771112233",
            IsActive = true
        };
        db.StaffMembers.Add(branchAdmin);
        var branchRole = await db.Roles.FirstAsync(r => r.Name == "Branch Admin");
        db.StaffRoles.Add(new StaffRole { StaffId = branchAdmin.Id, RoleId = branchRole.Id });
        await db.SaveChangesAsync();

        var req = new UpdateStaffStatusRequest { IsActive = false, Reason = "Attempt to deactivate last super admin" };

        // Attempting to deactivate the only active super admin
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            authService.UpdateStaffStatusAsync(superAdmin.Id, superAdmin.Id, req, "127.0.0.1")); // Self check triggers first

        // When branch admin attempts it, branch admin cannot touch super admin
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.UpdateStaffStatusAsync(branchAdmin.Id, superAdmin.Id, req, "127.0.0.1"));
    }

    [Fact]
    public async Task StaffStatus_DeactivationRequiresReason_InvalidatesSecurityStamp_AndCanBeReactivated()
    {
        var (db, authService, _, _) = CreateTestContext();
        await authService.SeedInitialSuperAdminAsync();

        var superAdmin = await db.StaffMembers.FirstAsync(s => s.Email == "admin@sjewls.lk");

        // 1. Create a staff member
        var createdStaff = await authService.CreateStaffUserAsync(new CreateStaffUserRequest
        {
            FullName = "Staff Member To Deactivate",
            Email = "staff.deactivate@sjewls.lk",
            PhoneNumber = "+94775556677",
            Password = "StaffPassword@2026!",
            Role = "Staff"
        }, superAdmin.Id, "127.0.0.1");

        var initialStamp = (await db.StaffMembers.FindAsync(createdStaff.Id))!.SecurityStamp;

        // 2. Deactivating without reason throws ArgumentException
        var emptyReasonReq = new UpdateStaffStatusRequest { IsActive = false, Reason = "" };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            authService.UpdateStaffStatusAsync(superAdmin.Id, createdStaff.Id, emptyReasonReq, "127.0.0.1"));

        // 3. Deactivating with reason succeeds and invalidates SecurityStamp immediately
        var deactivateReq = new UpdateStaffStatusRequest { IsActive = false, Reason = "Staff left organization" };
        var deactivated = await authService.UpdateStaffStatusAsync(superAdmin.Id, createdStaff.Id, deactivateReq, "127.0.0.1");

        Assert.False(deactivated.IsActive);
        Assert.NotNull(deactivated.DeactivatedAtUtc);
        Assert.Equal("Staff left organization", deactivated.DeactivationReason);

        var dbStaff = await db.StaffMembers.FindAsync(createdStaff.Id);
        Assert.NotEqual(initialStamp, dbStaff!.SecurityStamp); // SecurityStamp regenerated!

        // Inactive staff cannot log in
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.LoginAsync(new StaffLoginRequest
            {
                UsernameOrEmail = "staff.deactivate@sjewls.lk",
                Password = "StaffPassword@2026!"
            }, "127.0.0.1"));

        // 4. Reactivation restores active status
        var activateReq = new UpdateStaffStatusRequest { IsActive = true, Reason = "Reinstated" };
        var reactivated = await authService.UpdateStaffStatusAsync(superAdmin.Id, createdStaff.Id, activateReq, "127.0.0.1");

        Assert.True(reactivated.IsActive);
        Assert.Null(reactivated.DeactivatedAtUtc);
        Assert.Null(reactivated.DeactivationReason);
    }
}
