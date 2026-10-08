using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SJewls.Application.DTOs;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class CustomerContactAndRegistrationFlowTests
{
    private static (SJewlsDbContext db, CustomerAuthService authService, OtpService otpService, FakeSmsSender fakeSms, FakeEmailOtpProvider fakeEmail) CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new SJewlsDbContext(options);

        // Seed Jaffna branch
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Code = "JAF-01",
            Name = "SJewls Jaffna Main Branch",
            Address = "124 Hospital Road",
            City = "Jaffna",
            Country = "Sri Lanka",
            Currency = "LKR",
            Timezone = "Asia/Colombo",
            IsActive = true
        };
        db.Branches.Add(branch);
        db.SaveChanges();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["App:Environment"] = "Development",
                ["App:TestOtp"] = "123456",
                ["Jwt:Secret"] = "super-secret-key-that-must-be-at-least-32-characters-long-sjewls-dev"
            })
            .Build();

        var fakeSms = new FakeSmsSender();
        var fakeEmail = new FakeEmailOtpProvider();
        var otpService = new OtpService(db, fakeSms, fakeEmail, config, NullLogger<OtpService>.Instance);
        var tokenService = new TokenService(config);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var authService = new CustomerAuthService(db, otpService, tokenService, auditService);

        return (db, authService, otpService, fakeSms, fakeEmail);
    }

    [Fact]
    public async Task CheckContactAsync_ReturnsNotExists_WhenCustomerNotInDb()
    {
        var (_, authService, _, _, _) = CreateTestContext();

        var response = await authService.CheckContactAsync("0769882118");

        Assert.False(response.Exists);
        Assert.False(response.IsProfileComplete);
        Assert.Equal("+94769882118", response.NormalizedContact);
        Assert.Equal(ContactType.Phone, response.ContactType);
        Assert.Equal("Register", response.NextAction);
    }

    [Fact]
    public async Task CheckContactAsync_ReturnsExists_WhenCustomerInDbWithCompletedProfile()
    {
        var (db, authService, _, _, _) = CreateTestContext();

        var branch = await db.Branches.FirstAsync();
        db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Existing User",
            PhoneNumber = "+94769882118",
            IsPhoneVerified = true,
            PrimaryBranchId = branch.Id,
            IsActive = true,
            IsProfileComplete = true
        });
        await db.SaveChangesAsync();

        var response = await authService.CheckContactAsync("+94769882118");

        Assert.True(response.Exists);
        Assert.True(response.IsProfileComplete);
        Assert.Equal("Login", response.NextAction);
    }

    [Fact]
    public async Task RequestOtp_IdentifiesNewCustomer_AndReturnsRegisterNextAction()
    {
        var (_, _, otpService, _, _) = CreateTestContext();

        var response = await otpService.RequestOtpAsync("0769882118");

        Assert.True(response.Success);
        Assert.False(response.IsExistingCustomer);
        Assert.Equal("Register", response.NextAction);
    }

    [Fact]
    public async Task RequestOtp_IdentifiesExistingCustomer_AndReturnsLoginNextAction()
    {
        var (db, _, otpService, _, _) = CreateTestContext();
        var branch = await db.Branches.FirstAsync();
        db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Active Customer",
            PhoneNumber = "+94769882118",
            PrimaryBranchId = branch.Id,
            IsActive = true,
            IsProfileComplete = true
        });
        await db.SaveChangesAsync();

        var response = await otpService.RequestOtpAsync("+94769882118");

        Assert.True(response.Success);
        Assert.True(response.IsExistingCustomer);
        Assert.Equal("Login", response.NextAction);
    }

    [Fact]
    public async Task Registration_SignsInWithPhone_AndRegistersWithEmail_SavesBothContacts()
    {
        // Scenario: Customer initially enters phone number (e.g. 0769882118),
        // verifies OTP, but during registration only provides email (anojan@gmail.com).
        // Result: Customer record in DB MUST have the initially entered phone number (+94769882118)
        // AND the email given at registration (anojan@gmail.com).
        var (db, authService, otpService, _, _) = CreateTestContext();

        // 1. Request OTP via Phone
        var reqResult = await otpService.RequestOtpAsync("0769882118");
        Assert.True(reqResult.Success);
        Assert.Equal("+94769882118", reqResult.NormalizedContact);

        // 2. Verify OTP
        var verifyResult = await authService.ProcessOtpVerificationAsync("0769882118", "123456", "127.0.0.1");
        Assert.Equal("CompleteProfile", verifyResult.NextAction);
        Assert.NotNull(verifyResult.RegistrationToken);
        Assert.Equal("+94769882118", verifyResult.VerifiedContact);

        // 3. Complete Registration with Email
        var regResult = await authService.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationToken = verifyResult.RegistrationToken,
            FullName = "Anojan",
            DateOfBirth = new DateOnly(2000, 8, 28),
            Nic = "200012637289",
            Email = "anojan@gmail.com"
        }, "127.0.0.1");

        // Assert: NextAction is Dashboard
        Assert.Equal("Dashboard", regResult.NextAction);
        Assert.NotNull(regResult.AccessToken);
        Assert.NotNull(regResult.Customer);

        // Assert: CustomerSummaryDto contains BOTH phone and email
        Assert.Equal("+94769882118", regResult.Customer.PhoneNumber);
        Assert.Equal("anojan@gmail.com", regResult.Customer.Email);
        Assert.Equal("Anojan", regResult.Customer.FullName);

        // Assert: Database Customer record contains BOTH
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Nic == "200012637289");
        Assert.NotNull(customer);
        Assert.Equal("+94769882118", customer.PhoneNumber);
        Assert.True(customer.IsPhoneVerified);
        Assert.Equal("anojan@gmail.com", customer.Email);
        Assert.True(customer.IsProfileComplete);

        // 4. Verify subsequent CheckContact finds the customer by phone AND by email
        var phoneCheck = await authService.CheckContactAsync("0769882118");
        Assert.True(phoneCheck.Exists);
        Assert.Equal("Login", phoneCheck.NextAction);

        var emailCheck = await authService.CheckContactAsync("anojan@gmail.com");
        Assert.True(emailCheck.Exists);
        Assert.Equal("Login", emailCheck.NextAction);
    }

    [Fact]
    public async Task Registration_SignsInWithEmail_AndRegistersWithPhone_SavesBothContacts()
    {
        // Scenario: Customer enters email to sign in, verifies OTP,
        // and provides phone number during registration.
        // Result: Customer record in DB MUST have the initially entered email
        // AND the phone number given at registration.
        var (db, authService, otpService, _, _) = CreateTestContext();

        // 1. Request OTP via Email
        var reqResult = await otpService.RequestOtpAsync("anojan@sjewls.lk");
        Assert.True(reqResult.Success);

        // 2. Verify OTP
        var verifyResult = await authService.ProcessOtpVerificationAsync("anojan@sjewls.lk", "123456", "127.0.0.1");
        Assert.Equal("CompleteProfile", verifyResult.NextAction);
        Assert.NotNull(verifyResult.RegistrationToken);

        // 3. Complete Registration with Phone Number
        var regResult = await authService.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationToken = verifyResult.RegistrationToken,
            FullName = "Anojan",
            DateOfBirth = new DateOnly(2000, 8, 28),
            Nic = "200012637289",
            PhoneNumber = "0769882118"
        }, "127.0.0.1");

        Assert.Equal("Dashboard", regResult.NextAction);
        Assert.Equal("anojan@sjewls.lk", regResult.Customer?.Email);
        Assert.Equal("+94769882118", regResult.Customer?.PhoneNumber);

        // Check DB customer
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Nic == "200012637289");
        Assert.NotNull(customer);
        Assert.Equal("anojan@sjewls.lk", customer.Email);
        Assert.True(customer.IsEmailVerified);
        Assert.Equal("+94769882118", customer.PhoneNumber);
    }

    [Fact]
    public async Task Registration_SignsInWithPhone_AndRegistersWithoutSecondaryContact_SavesInitialPhone()
    {
        // Per specification: customers can register with only one contact and add second later
        var (db, authService, otpService, _, _) = CreateTestContext();

        // 1. Request OTP via Phone
        await otpService.RequestOtpAsync("0769882118");

        // 2. Verify OTP
        var verifyResult = await authService.ProcessOtpVerificationAsync("0769882118", "123456", "127.0.0.1");

        // 3. Complete Registration without Email
        var regResult = await authService.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationToken = verifyResult.RegistrationToken!,
            FullName = "Single Contact Customer",
            DateOfBirth = new DateOnly(1995, 5, 15),
            Nic = "199513507890"
        }, "127.0.0.1");

        Assert.Equal("Dashboard", regResult.NextAction);
        Assert.Equal("+94769882118", regResult.Customer?.PhoneNumber);
        Assert.Null(regResult.Customer?.Email);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Nic == "199513507890");
        Assert.NotNull(customer);
        Assert.Equal("+94769882118", customer.PhoneNumber);
        Assert.True(customer.IsPhoneVerified);
        Assert.Null(customer.Email);
    }

    [Fact]
    public async Task Registration_SupportsAdditionalContact_WhenEmailIsGivenInAdditionalContact()
    {
        // Backward compatibility: client passes email in additionalContact field
        var (db, authService, otpService, _, _) = CreateTestContext();

        await otpService.RequestOtpAsync("0769882118");
        var verifyResult = await authService.ProcessOtpVerificationAsync("0769882118", "123456", "127.0.0.1");

        var regResult = await authService.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationToken = verifyResult.RegistrationToken!,
            FullName = "Legacy Client Customer",
            DateOfBirth = new DateOnly(1998, 1, 1),
            Nic = "199800109999",
            AdditionalContact = "legacy@example.com"
        }, "127.0.0.1");

        Assert.Equal("Dashboard", regResult.NextAction);
        Assert.Equal("+94769882118", regResult.Customer?.PhoneNumber);
        Assert.Equal("legacy@example.com", regResult.Customer?.Email);
    }

    [Fact]
    public async Task AdminCreatedCustomer_OnFirstOtpVerification_LinksExistingRecord_AndCompletesProfile()
    {
        var (db, authService, otpService, _, _) = CreateTestContext();

        // 1. Admin creates customer (unverified contacts, profile incomplete)
        var branch = await db.Branches.FirstAsync();
        var adminCustomer = new Customer
        {
            FullName = "Admin Created Customer",
            PhoneNumber = "+94770011223",
            Email = "admincreated@example.com",
            Nic = "199411223344",
            PrimaryBranchId = branch.Id,
            IsPhoneVerified = false,
            IsEmailVerified = false,
            IsActive = true,
            IsProfileComplete = false
        };
        db.Customers.Add(adminCustomer);
        await db.SaveChangesAsync();

        // 2. Customer checks contact
        var check = await authService.CheckContactAsync("+94770011223");
        Assert.True(check.Exists);
        Assert.False(check.IsProfileComplete);
        Assert.Equal("CompleteProfile", check.NextAction);

        // 3. Customer requests and verifies OTP
        await otpService.RequestOtpAsync("+94770011223");
        var verify = await authService.ProcessOtpVerificationAsync("+94770011223", "123456", "127.0.0.1");

        Assert.Equal("CompleteProfile", verify.NextAction);
        Assert.NotNull(verify.RegistrationToken);

        // Verify phone marked verified on existing customer record
        var customerAfterOtp = await db.Customers.FindAsync(adminCustomer.Id);
        Assert.True(customerAfterOtp!.IsPhoneVerified);

        // 4. Customer submits Date of Birth to complete profile
        var complete = await authService.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationToken = verify.RegistrationToken,
            FullName = "Admin Created Customer",
            DateOfBirth = new DateOnly(1994, 6, 20),
            Nic = "199411223344"
        }, "127.0.0.1");

        Assert.Equal("Dashboard", complete.NextAction);
        Assert.NotNull(complete.AccessToken);

        // 5. Assert that no duplicate customer was created!
        var allCustomers = await db.Customers.Where(c => c.Nic == "199411223344").ToListAsync();
        Assert.Single(allCustomers);
        Assert.Equal(adminCustomer.Id, allCustomers[0].Id);
        Assert.True(allCustomers[0].IsProfileComplete);
        Assert.Equal(new DateOnly(1994, 6, 20), allCustomers[0].DateOfBirth);
    }

    [Fact]
    public async Task DeactivatedCustomer_CannotLoginOrVerifyOtp()
    {
        var (db, authService, otpService, _, _) = CreateTestContext();

        var branch = await db.Branches.FirstAsync();
        var deactivatedCustomer = new Customer
        {
            FullName = "Deactivated Customer",
            PhoneNumber = "+94778889999",
            Email = "deactivated@example.com",
            Nic = "199122334455",
            PrimaryBranchId = branch.Id,
            IsPhoneVerified = true,
            IsEmailVerified = true,
            IsActive = false, // Deactivated!
            IsProfileComplete = true,
            DeactivationReason = "Admin suspension"
        };
        db.Customers.Add(deactivatedCustomer);
        await db.SaveChangesAsync();

        // 1. CheckContact reflects deactivated status
        var check = await authService.CheckContactAsync("+94778889999");
        Assert.Equal("Deactivated", check.NextAction);

        // 2. Request OTP and attempt verification throws UnauthorizedAccessException
        await otpService.RequestOtpAsync("+94778889999");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.ProcessOtpVerificationAsync("+94778889999", "123456", "127.0.0.1"));

        // 3. Get profile throws UnauthorizedAccessException
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.GetCustomerProfileAsync(deactivatedCustomer.Id));
    }

    [Fact]
    public async Task CustomerSelfServiceAccountClosure_RequiresClosureOtp_DeactivatesAndRevokesTokens()
    {
        var (db, authService, otpService, _, _) = CreateTestContext();

        var branch = await db.Branches.FirstAsync();
        var customer = new Customer
        {
            FullName = "Closing Customer",
            PhoneNumber = "+94774443322",
            Email = "closing@example.com",
            Nic = "199655443322",
            PrimaryBranchId = branch.Id,
            IsPhoneVerified = true,
            IsEmailVerified = true,
            IsActive = true,
            IsProfileComplete = true
        };
        db.Customers.Add(customer);

        var token = new RefreshToken
        {
            CustomerId = customer.Id,
            Token = "active-closing-refresh-token",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30),
            IsRevoked = false
        };
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();

        // 1. Request closure OTP
        var reqResult = await authService.RequestAccountClosureOtpAsync(customer.Id);
        Assert.True(reqResult.Success);
        Assert.Equal("SMS", reqResult.DeliveryChannel);
        Assert.NotEmpty(reqResult.MaskedContact);

        // 2. A LoginOrRegister OTP cannot authorize account closure!
        await otpService.RequestOtpAsync("+94774443322", purpose: "LoginOrRegister");
        // Verify with closure challenge requires valid challenge created for AccountClosure
        // Attempting with wrong code fails
        await Assert.ThrowsAsync<ArgumentException>(() =>
            authService.ConfirmAccountClosureAsync(customer.Id, new ConfirmAccountClosureRequest
            {
                Code = "999999",
                Reason = "I want to delete my account"
            }, "127.0.0.1"));

        // 3. Confirm closure with valid OTP
        var confirmResult = await authService.ConfirmAccountClosureAsync(customer.Id, new ConfirmAccountClosureRequest
        {
            Code = "123456",
            Reason = "No longer using jewellery plans"
        }, "127.0.0.1");

        Assert.True(confirmResult.Success);
        Assert.Equal("Inactive", confirmResult.Status);
        Assert.Contains("retained", confirmResult.Message);

        // 4. Verify DB state: soft closed, records retained
        var dbCustomer = await db.Customers.FindAsync(customer.Id);
        Assert.NotNull(dbCustomer);
        Assert.False(dbCustomer.IsActive);
        Assert.Equal("CustomerRequestedClosure", dbCustomer.ClosureReason);
        Assert.NotNull(dbCustomer.ClosedAtUtc);
        Assert.Equal("CustomerRequestedClosure", dbCustomer.DeactivationReason);

        // Refresh tokens revoked
        var dbToken = await db.RefreshTokens.FirstAsync(t => t.Token == "active-closing-refresh-token");
        Assert.True(dbToken.IsRevoked);
    }

    [Fact]
    public async Task AdminCreatedCustomer_CanCompleteProfile_ProvidingOnlyDateOfBirth()
    {
        var (db, authService, otpService, _, _) = CreateTestContext();
        var branch = await db.Branches.FirstAsync();

        var adminCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Customer Prepopulated",
            PhoneNumber = "+94771122334",
            Email = "adminpre@example.com",
            Nic = "199412345678",
            PrimaryBranchId = branch.Id,
            IsPhoneVerified = false,
            IsEmailVerified = false,
            IsActive = true,
            IsProfileComplete = false
        };
        db.Customers.Add(adminCustomer);
        await db.SaveChangesAsync();

        // Check contact
        var check = await authService.CheckContactAsync("adminpre@example.com");
        Assert.True(check.Exists);
        Assert.False(check.IsProfileComplete);
        Assert.Equal("CompleteProfile", check.NextAction);

        // Request and verify OTP
        await otpService.RequestOtpAsync("adminpre@example.com");
        var verify = await authService.ProcessOtpVerificationAsync("adminpre@example.com", "123456", "127.0.0.1");
        Assert.Equal("CompleteProfile", verify.NextAction);
        Assert.NotNull(verify.RegistrationToken);
        Assert.NotNull(verify.Customer);
        Assert.Equal("Admin Customer Prepopulated", verify.Customer.FullName);

        // Complete profile providing ONLY DateOfBirth
        var complete = await authService.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationToken = verify.RegistrationToken,
            DateOfBirth = new DateOnly(1994, 6, 15)
        }, "127.0.0.1");

        Assert.Equal("Dashboard", complete.NextAction);
        Assert.NotNull(complete.AccessToken);

        var updated = await db.Customers.FindAsync(adminCustomer.Id);
        Assert.True(updated!.IsProfileComplete);
        Assert.Equal("Admin Customer Prepopulated", updated.FullName);
        Assert.Equal("199412345678", updated.Nic);
        Assert.Equal("+94771122334", updated.PhoneNumber);
        Assert.Equal("adminpre@example.com", updated.Email);
        Assert.True(updated.IsEmailVerified);
    }
}


