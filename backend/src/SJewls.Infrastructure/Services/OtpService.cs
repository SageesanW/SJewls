using System.Security.Cryptography;
using System.Text.Json;
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

public class AuditLogService : IAuditLogService
{
    private readonly SJewlsDbContext _db;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(SJewlsDbContext db, ILogger<AuditLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogActionAsync(string actorType, Guid? actorId, string action, string targetEntity, string targetId, Guid? branchId, object? before, object? after, string? ipAddress)
    {
        try
        {
            var audit = new AuditLog
            {
                ActorType = actorType,
                ActorId = actorId,
                Action = action,
                TargetEntity = targetEntity,
                TargetId = targetId,
                BranchId = branchId,
                BeforeJson = before != null ? JsonSerializer.Serialize(before) : null,
                AfterJson = after != null ? JsonSerializer.Serialize(after) : null,
                IpAddress = ipAddress
            };

            _db.AuditLogs.Add(audit);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit log for action {Action} on {TargetEntity} {TargetId}", action, targetEntity, targetId);
        }
    }
}

public class OtpService : IOtpService
{
    private readonly SJewlsDbContext _db;
    private readonly ISmsSender _smsSender;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;
    private readonly ILogger<OtpService> _logger;

    private const int OtpExpiryMinutes = 5;
    private const int ResendCooldownSeconds = 60;
    private const int MaxAllowedAttempts = 5;

    public OtpService(
        SJewlsDbContext db,
        ISmsSender smsSender,
        IEmailSender emailSender,
        IConfiguration config,
        ILogger<OtpService> logger)
    {
        _db = db;
        _smsSender = smsSender;
        _emailSender = emailSender;
        _config = config;
        _logger = logger;
    }

    public async Task<RequestOtpResponse> RequestOtpAsync(string inputContact, string purpose = "LoginOrRegister", Guid? customerId = null)
    {
        if (!ValidationHelper.TryNormalizeContact(inputContact, out var normalizedContact, out var contactType))
        {
            return new RequestOtpResponse
            {
                Success = false,
                Message = "Invalid phone number or email address format."
            };
        }

        var now = DateTimeOffset.UtcNow;

        // Check for active cooldown on existing pending challenges
        var existingChallenge = await _db.OtpChallenges
            .Where(o => o.ContactValue == normalizedContact && o.Purpose == purpose && !o.IsConsumed && o.ExpiresAtUtc > now)
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (existingChallenge != null && existingChallenge.ResendCooldownUntilUtc > now)
        {
            var remainingCooldown = (int)(existingChallenge.ResendCooldownUntilUtc - now).TotalSeconds;
            return new RequestOtpResponse
            {
                Success = false,
                Message = $"Please wait {remainingCooldown} seconds before requesting a new verification code.",
                NormalizedContact = normalizedContact,
                ContactType = contactType,
                CooldownSeconds = remainingCooldown,
                ExpiresInSeconds = (int)(existingChallenge.ExpiresAtUtc - now).TotalSeconds
            };
        }

        // Determine OTP code: In Dev/Demo use configured TestOtp if set, else crypto random
        var isDevOrDemo = _config["ASPNETCORE_ENVIRONMENT"] == "Development" || _config["App:Environment"] == "Development";
        string otpCode;

        if (isDevOrDemo && !string.IsNullOrWhiteSpace(_config["App:TestOtp"]))
        {
            otpCode = _config["App:TestOtp"]!;
        }
        else
        {
            otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        }

        // Invalidate any older unused challenges for this contact and purpose
        var olderChallenges = await _db.OtpChallenges
            .Where(o => o.ContactValue == normalizedContact && o.Purpose == purpose && !o.IsConsumed)
            .ToListAsync();

        foreach (var old in olderChallenges)
        {
            old.IsConsumed = true;
        }

        var challenge = new OtpChallenge
        {
            ContactValue = normalizedContact,
            ContactType = contactType,
            Purpose = purpose,
            Code = otpCode,
            ExpiresAtUtc = now.AddMinutes(OtpExpiryMinutes),
            ResendCooldownUntilUtc = now.AddSeconds(ResendCooldownSeconds),
            AttemptCount = 0,
            MaxAttempts = MaxAllowedAttempts,
            IsConsumed = false,
            CustomerId = customerId
        };

        _db.OtpChallenges.Add(challenge);
        await _db.SaveChangesAsync();

        // Dispatch via appropriate channel
        var messageText = $"Your SJewls verification code is {otpCode}. Valid for {OtpExpiryMinutes} minutes. Never share this code with anyone.";

        if (contactType == ContactType.Phone)
        {
            await _smsSender.SendSmsAsync(normalizedContact, messageText);
        }
        else
        {
            await _emailSender.SendEmailAsync(normalizedContact, "SJewls Verification Code", messageText);
        }

        _logger.LogInformation("Generated OTP {Code} for contact {Contact} ({Type}) with purpose {Purpose}", otpCode, normalizedContact, contactType, purpose);

        return new RequestOtpResponse
        {
            Success = true,
            Message = "Verification code sent successfully.",
            NormalizedContact = normalizedContact,
            ContactType = contactType,
            ExpiresInSeconds = OtpExpiryMinutes * 60,
            CooldownSeconds = ResendCooldownSeconds,
            DevOtp = isDevOrDemo ? otpCode : null
        };
    }

    public async Task<(bool Success, string Message, string NormalizedContact, ContactType ContactType, Guid? CustomerId)> VerifyOtpAsync(string contact, string code, string purpose = "LoginOrRegister", Guid? customerId = null)
    {
        if (!ValidationHelper.TryNormalizeContact(contact, out var normalizedContact, out var contactType))
        {
            return (false, "Invalid contact format.", string.Empty, contactType, null);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return (false, "Verification code is required.", normalizedContact, contactType, null);
        }

        var now = DateTimeOffset.UtcNow;

        var challengeQuery = _db.OtpChallenges
            .Where(o => o.ContactValue == normalizedContact && o.Purpose == purpose && !o.IsConsumed);

        if (customerId.HasValue)
        {
            challengeQuery = challengeQuery.Where(o => o.CustomerId == customerId.Value);
        }

        var challenge = await challengeQuery
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (challenge == null)
        {
            return (false, "No active verification challenge found. Please request a new code.", normalizedContact, contactType, null);
        }

        if (challenge.ExpiresAtUtc < now)
        {
            challenge.IsConsumed = true;
            await _db.SaveChangesAsync();
            return (false, "Verification code has expired. Please request a new one.", normalizedContact, contactType, null);
        }

        challenge.AttemptCount++;

        if (challenge.AttemptCount > challenge.MaxAttempts)
        {
            challenge.IsConsumed = true;
            await _db.SaveChangesAsync();
            return (false, "Maximum verification attempts exceeded. Please request a new code.", normalizedContact, contactType, null);
        }

        if (!string.Equals(challenge.Code.Trim(), code.Trim(), StringComparison.Ordinal))
        {
            await _db.SaveChangesAsync();
            var remainingAttempts = challenge.MaxAttempts - challenge.AttemptCount;
            return (false, $"Incorrect verification code. {remainingAttempts} attempts remaining.", normalizedContact, contactType, null);
        }

        // Single-use: Mark consumed immediately
        challenge.IsConsumed = true;
        await _db.SaveChangesAsync();

        return (true, "Verified successfully.", normalizedContact, contactType, challenge.CustomerId);
    }
}
