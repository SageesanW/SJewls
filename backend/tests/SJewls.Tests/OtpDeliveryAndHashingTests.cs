using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SJewls.Application.Common;
using SJewls.Application.Interfaces;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class FakeSmsSender : ISmsSender
{
    public string? LastPhoneNumber { get; private set; }
    public string? LastMessage { get; private set; }
    public int SendCount { get; private set; }

    public Task SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        LastPhoneNumber = phoneNumber;
        LastMessage = message;
        SendCount++;
        return Task.CompletedTask;
    }
}

public class FakeEmailOtpProvider : IEmailOtpProvider
{
    public string? LastRecipient { get; private set; }
    public string? LastOtpCode { get; private set; }
    public int LastExpiryMinutes { get; private set; }
    public int SendCount { get; private set; }

    public Task SendOtpEmailAsync(string toEmail, string otpCode, int expiryMinutes, CancellationToken cancellationToken = default)
    {
        LastRecipient = toEmail;
        LastOtpCode = otpCode;
        LastExpiryMinutes = expiryMinutes;
        SendCount++;
        return Task.CompletedTask;
    }
}

public class OtpDeliveryAndHashingTests
{
    private static SJewlsDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new SJewlsDbContext(options);
    }

    [Fact]
    public async Task RequestOtpAsync_Phone_SendsGeneratedPlaintextOtp_And_StoresOnlyHashInDb()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var fakeSms = new FakeSmsSender();
        var fakeEmail = new FakeEmailOtpProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production", // forces crypto random generation
                ["App:Environment"] = "Production"
            })
            .Build();

        var otpService = new OtpService(db, fakeSms, fakeEmail, config, NullLogger<OtpService>.Instance);
        var phoneNumber = "+94771234567";

        // Act
        var result = await otpService.RequestOtpAsync(phoneNumber);

        // Assert: 1. Success returned
        Assert.True(result.Success);
        Assert.Equal(1, fakeSms.SendCount);
        Assert.Equal(phoneNumber, fakeSms.LastPhoneNumber);

        // Assert: 2. Outgoing SMS contains the plaintext OTP, not empty, not a template placeholder
        Assert.NotNull(fakeSms.LastMessage);
        Assert.StartsWith("Your SJewls verification code is ", fakeSms.LastMessage);
        Assert.Contains(". Valid for 5 minutes. Never share this code with anyone.", fakeSms.LastMessage);

        // Extract OTP from message
        var parts = fakeSms.LastMessage.Split("Your SJewls verification code is ");
        var codePart = parts[1].Split(". Valid for")[0];
        Assert.Equal(6, codePart.Length);
        Assert.True(codePart.All(char.IsDigit), "OTP must be numeric string");

        // Assert: 3. The database stores ONLY the secure cryptographic hash, NOT the plaintext code
        var challenge = await db.OtpChallenges.FirstOrDefaultAsync(o => o.ContactValue == phoneNumber);
        Assert.NotNull(challenge);

        var expectedHash = OtpService.HashOtp(codePart);
        Assert.Equal(expectedHash, challenge.Code);
        Assert.NotEqual(codePart, challenge.Code); // Plaintext is NOT stored in DB!
        Assert.Equal(64, challenge.Code.Length);   // SHA-256 hex is 64 characters

        // Assert: 4. The outgoing message does NOT contain the SHA-256 hash or empty placeholder
        Assert.DoesNotContain(expectedHash, fakeSms.LastMessage);
        Assert.DoesNotContain("{otpCode}", fakeSms.LastMessage);

        // Assert: 5. Verification succeeds using the plaintext code received in the message
        var (verifySuccess, _, _, _, _) = await otpService.VerifyOtpAsync(phoneNumber, codePart);
        Assert.True(verifySuccess);

        // Assert: 6. Challenge is marked as consumed (single-use)
        var consumedChallenge = await db.OtpChallenges.FirstOrDefaultAsync(o => o.ContactValue == phoneNumber);
        Assert.True(consumedChallenge?.IsConsumed);
    }

    [Fact]
    public async Task RequestOtpAsync_Email_SendsGeneratedPlaintextOtp_And_StoresOnlyHashInDb()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var fakeSms = new FakeSmsSender();
        var fakeEmail = new FakeEmailOtpProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["App:Environment"] = "Production"
            })
            .Build();

        var otpService = new OtpService(db, fakeSms, fakeEmail, config, NullLogger<OtpService>.Instance);
        var email = "customer@sjewls.lk";

        // Act
        var result = await otpService.RequestOtpAsync(email);

        // Assert: 1. Success returned
        Assert.True(result.Success);
        Assert.Equal(1, fakeEmail.SendCount);
        Assert.Equal(email, fakeEmail.LastRecipient);

        // Assert: 2. Outgoing OTP is a 6-digit string, NOT a hash
        Assert.NotNull(fakeEmail.LastOtpCode);
        Assert.Equal(6, fakeEmail.LastOtpCode.Length);
        Assert.True(fakeEmail.LastOtpCode.All(char.IsDigit));

        // Assert: 3. Database stores ONLY the secure hash
        var challenge = await db.OtpChallenges.FirstOrDefaultAsync(o => o.ContactValue == email);
        Assert.NotNull(challenge);

        var expectedHash = OtpService.HashOtp(fakeEmail.LastOtpCode);
        Assert.Equal(expectedHash, challenge.Code);
        Assert.NotEqual(fakeEmail.LastOtpCode, challenge.Code);

        // Assert: 4. Verification succeeds against the hash
        var (verifySuccess, _, _, _, _) = await otpService.VerifyOtpAsync(email, fakeEmail.LastOtpCode);
        Assert.True(verifySuccess);
    }

    [Fact]
    public async Task OtpCode_PreservesLeadingZeros_AndInsertsIntoMessage()
    {
        // Arrange: configure test OTP with leading zeros
        using var db = CreateInMemoryDbContext();
        var fakeSms = new FakeSmsSender();
        var fakeEmail = new FakeEmailOtpProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["App:Environment"] = "Development",
                ["App:TestOtp"] = "007890" // starts with leading zeros
            })
            .Build();

        var otpService = new OtpService(db, fakeSms, fakeEmail, config, NullLogger<OtpService>.Instance);
        var phoneNumber = "+94770000001";

        // Act
        var result = await otpService.RequestOtpAsync(phoneNumber);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(fakeSms.LastMessage);

        // Leading zeros MUST be preserved as string in message body
        Assert.Contains("007890", fakeSms.LastMessage);
        Assert.DoesNotContain("7890", fakeSms.LastMessage.Replace("007890", "MATCH"));

        // Verify that hash of "007890" is stored
        var challenge = await db.OtpChallenges.FirstOrDefaultAsync(o => o.ContactValue == phoneNumber);
        Assert.NotNull(challenge);
        Assert.Equal(OtpService.HashOtp("007890"), challenge.Code);

        // Verify that submitting "007890" succeeds
        var (verifySuccess, _, _, _, _) = await otpService.VerifyOtpAsync(phoneNumber, "007890");
        Assert.True(verifySuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task EmailOtpProvider_RejectsEmptyOtpCode(string? emptyCode)
    {
        // Arrange
        var options = Options.Create(new EmailOptions
        {
            Host = "smtp.gmail.com",
            Port = 587,
            Username = "test@gmail.com",
            Password = "app-password",
            FromAddress = "test@gmail.com",
            FromName = "SJewls"
        });

        var provider = new EmailOtpProvider(options, NullLogger<EmailOtpProvider>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.SendOtpEmailAsync("recipient@example.com", emptyCode!, 5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task TextLkSmsSender_RejectsEmptyMessage(string? emptyMessage)
    {
        // Arrange
        var options = Options.Create(new SmsOptions
        {
            ApiUrl = "https://app.text.lk/api/v3/sms/send",
            ApiToken = "fake-token",
            SenderId = "TextLKDemo"
        });

        using var httpClient = new HttpClient();
        var smsSender = new TextLkSmsSender(httpClient, options, NullLogger<TextLkSmsSender>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            smsSender.SendSmsAsync("94759712375", emptyMessage!));
    }

    [Fact]
    public void HashOtp_ProducesDeterministicSha256_AndVerifyOtpHash_MatchesConstantTime()
    {
        // Arrange
        var plainOtp = "654321";

        // Act
        var hash1 = OtpService.HashOtp(plainOtp);
        var hash2 = OtpService.HashOtp(plainOtp);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
        Assert.True(OtpService.VerifyOtpHash(plainOtp, hash1));
        Assert.False(OtpService.VerifyOtpHash("000000", hash1));
    }

    [Fact]
    public async Task RequestOtpAsync_GeneratesCryptographicallyRandomOtps_NotHardcoded123456()
    {
        // Arrange: default development configuration without any App:TestOtp configured
        using var db = CreateInMemoryDbContext();
        var fakeSms = new FakeSmsSender();
        var fakeEmail = new FakeEmailOtpProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development"
            })
            .Build();

        var otpService = new OtpService(db, fakeSms, fakeEmail, config, NullLogger<OtpService>.Instance);
        var generatedOtps = new HashSet<string>();

        // Act: Generate OTPs for different contacts
        for (int i = 0; i < 5; i++)
        {
            var phone = $"+9477000000{i}";
            var result = await otpService.RequestOtpAsync(phone);
            Assert.True(result.Success);
            Assert.NotNull(fakeSms.LastMessage);

            // Extract the 6-digit OTP code from SMS
            var parts = fakeSms.LastMessage.Split("Your SJewls verification code is ");
            var code = parts[1].Split(". Valid for")[0];

            Assert.Equal(6, code.Length);
            Assert.True(code.All(char.IsDigit));
            generatedOtps.Add(code);
        }

        // Assert: OTPs are random and varied (not just 123456 repeated)
        Assert.True(generatedOtps.Count > 1, "OTP values must be random and unique across requests");
    }
}
