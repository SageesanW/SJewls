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
}
