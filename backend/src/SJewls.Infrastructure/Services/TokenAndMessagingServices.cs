using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;

namespace SJewls.Infrastructure.Services;

public class MockSmsSender : ISmsSender
{
    private readonly ILogger<MockSmsSender> _logger;

    public MockSmsSender(ILogger<MockSmsSender> logger)
    {
        _logger = logger;
    }

    public Task SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("==================================================");
        _logger.LogInformation("[MOCK SMS SENT] To: {PhoneNumber} | Message: {Message}", phoneNumber, message);
        _logger.LogInformation("==================================================");
        return Task.CompletedTask;
    }
}

public class MockEmailSender : IEmailSender
{
    private readonly ILogger<MockEmailSender> _logger;

    public MockEmailSender(ILogger<MockEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("==================================================");
        _logger.LogInformation("[MOCK EMAIL SENT] To: {Email} | Subject: {Subject} | Body: {Body}", toEmail, subject, body);
        _logger.LogInformation("==================================================");
        return Task.CompletedTask;
    }
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateAccessToken(Customer customer, string branchCode)
    {
        var secret = _config["Jwt:Secret"] ?? "super-secret-key-that-must-be-at-least-32-characters-long-sjewls-dev";
        var issuer = _config["Jwt:Issuer"] ?? "SJewls.Api";
        var audience = _config["Jwt:Audience"] ?? "SJewls.App";
        var expiryMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var exp) ? exp : 1440;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, customer.FullName),
            new(ClaimTypes.Role, "Customer"),
            new("branch_code", branchCode),
            new("is_profile_complete", customer.IsProfileComplete.ToString().ToLower()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public string GenerateRegistrationToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return "reg_" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
