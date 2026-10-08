using SJewls.Application.DTOs;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;

namespace SJewls.Application.Interfaces;

public interface ISmsSender
{
    Task SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}

public interface IEmailOtpProvider
{
    Task SendOtpEmailAsync(string toEmail, string otpCode, int expiryMinutes, CancellationToken cancellationToken = default);
}

public interface ITokenService
{
    string GenerateAccessToken(Customer customer, string branchCode);
    string GenerateRefreshToken();
    string GenerateRegistrationToken();
}

public interface IOtpService
{
    Task<RequestOtpResponse> RequestOtpAsync(string inputContact, string purpose = "LoginOrRegister", Guid? customerId = null);
    Task<(bool Success, string Message, string NormalizedContact, ContactType ContactType, Guid? CustomerId)> VerifyOtpAsync(string contact, string code, string purpose = "LoginOrRegister", Guid? customerId = null);
}

public interface ICustomerAuthService
{
    Task<CheckContactResponse> CheckContactAsync(string contact);
    Task<VerifyOtpResponse> ProcessOtpVerificationAsync(string contact, string code, string? ipAddress);
    Task<VerifyOtpResponse> CompleteRegistrationAsync(CompleteRegistrationRequest request, string? ipAddress);
    Task<RefreshTokenResponse> RefreshTokenAsync(string refreshToken, string? ipAddress);
    Task LogoutAsync(string refreshToken, string? ipAddress);
    Task<CustomerProfileDto> GetCustomerProfileAsync(Guid customerId);
    Task<RequestOtpResponse> RequestSecondaryContactOtpAsync(Guid customerId, string contact);
    Task<bool> VerifySecondaryContactOtpAsync(Guid customerId, string contact, string code);
}

public interface IAuditLogService
{
    Task LogActionAsync(string actorType, Guid? actorId, string action, string targetEntity, string targetId, Guid? branchId, object? before, object? after, string? ipAddress);
}
