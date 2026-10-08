using System.ComponentModel.DataAnnotations;
using SJewls.Domain.Enums;

namespace SJewls.Application.DTOs;

public record CheckContactRequest
{
    [Required]
    public string Contact { get; init; } = string.Empty; // Phone (+94...) or Email
}

public record CheckContactResponse
{
    public bool Exists { get; init; }
    public bool IsProfileComplete { get; init; }
    public string NormalizedContact { get; init; } = string.Empty;
    public ContactType ContactType { get; init; }
    public string NextAction { get; init; } = string.Empty; // "Login" or "Register"
    public string Message { get; init; } = string.Empty;
}

public record RequestOtpRequest
{
    [Required]
    public string Contact { get; init; } = string.Empty; // Phone (+94...) or Email
}

public record RequestOtpResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string NormalizedContact { get; init; } = string.Empty;
    public ContactType ContactType { get; init; }
    public int ExpiresInSeconds { get; init; }
    public int CooldownSeconds { get; init; }
    public string? DevOtp { get; init; } // Only populated in non-production environments
    public bool IsExistingCustomer { get; init; }
    public string NextAction { get; init; } = "Login"; // "Login" or "Register"
}

public record VerifyOtpRequest
{
    [Required]
    public string Contact { get; init; } = string.Empty;

    [Required]
    [StringLength(10, MinimumLength = 4)]
    public string Code { get; init; } = string.Empty;
}

public record VerifyOtpResponse
{
    public string NextAction { get; init; } = string.Empty; // "Dashboard" or "CompleteProfile"
    
    // When NextAction == "Dashboard"
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public int? ExpiresInSeconds { get; init; }
    public CustomerSummaryDto? Customer { get; init; }

    // When NextAction == "CompleteProfile"
    public string? RegistrationToken { get; init; }
    public int? RegistrationTokenExpiresInSeconds { get; init; }
    public string? VerifiedContact { get; init; }
    public ContactType? VerifiedContactType { get; init; }
    public string? RequiredAdditionalContactType { get; init; } // "Email" if verified was Phone, or "Phone" if verified was Email
}

public record CompleteRegistrationRequest
{
    [Required]
    public string RegistrationToken { get; init; } = string.Empty;

    [StringLength(100, MinimumLength = 2)]
    public string? FullName { get; init; }

    [Required]
    public DateOnly DateOfBirth { get; init; }

    public string? Nic { get; init; } // Sri Lankan NIC (9 digits + V/X or 12 digits)

    public string? Email { get; init; } // Optional email if registering or supplementing

    public string? PhoneNumber { get; init; } // Optional phone if registering or supplementing

    public string? AdditionalContact { get; init; } // Backward-compatible contact field
}

public record CustomerSummaryDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Nic { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public string PrimaryContact { get; init; } = string.Empty;
    public string PrimaryBranchCode { get; init; } = "JAF-01";
    public bool IsProfileComplete { get; init; }
}

public record CustomerProfileDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string? Nic { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public bool IsPhoneVerified { get; init; }
    public bool IsEmailVerified { get; init; }
    public DateTimeOffset? PhoneVerifiedAtUtc { get; init; }
    public DateTimeOffset? EmailVerifiedAtUtc { get; init; }
    public Guid PrimaryBranchId { get; init; }
    public string PrimaryBranchCode { get; init; } = string.Empty;
    public string PrimaryBranchName { get; init; } = string.Empty;
    public bool IsProfileComplete { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public record CustomerContactDto
{
    public Guid Id { get; init; }
    public ContactType Type { get; init; }
    public string Value { get; init; } = string.Empty;
    public bool IsVerified { get; init; }
    public DateTimeOffset? VerifiedAtUtc { get; init; }
    public bool IsPrimary { get; init; }
}

public record RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

public record RefreshTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public int ExpiresInSeconds { get; init; }
}

public record LogoutRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

public record RequestSecondaryContactOtpRequest
{
    [Required]
    public string Contact { get; init; } = string.Empty;
}

public record VerifySecondaryContactOtpRequest
{
    [Required]
    public string Contact { get; init; } = string.Empty;

    [Required]
    public string Code { get; init; } = string.Empty;
}

public record RequestClosureOtpResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string DeliveryChannel { get; init; } = string.Empty;
    public string MaskedContact { get; init; } = string.Empty;
    public int ExpiresInSeconds { get; init; }
    public int CooldownSeconds { get; init; }
    public string? DevOtp { get; init; }
}

public record ConfirmAccountClosureRequest
{
    [Required(ErrorMessage = "Verification code is required.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Verification code must be 6 digits.")]
    public string Code { get; init; } = string.Empty;

    public string? Reason { get; init; }
}

public record AccountClosureResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset ClosedAtUtc { get; init; }
    public string Status { get; init; } = "Inactive";
}

