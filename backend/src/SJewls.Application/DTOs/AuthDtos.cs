using System.ComponentModel.DataAnnotations;
using SJewls.Domain.Enums;

namespace SJewls.Application.DTOs;

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

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    public DateOnly DateOfBirth { get; init; }

    [Required]
    public string Nic { get; init; } = string.Empty; // Sri Lankan NIC (9 digits + V/X or 12 digits)

    [Required]
    public string AdditionalContact { get; init; } = string.Empty; // Email if initial was Phone, or Phone if initial was Email
}

public record CustomerSummaryDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
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
    public Guid PrimaryBranchId { get; init; }
    public string PrimaryBranchCode { get; init; } = string.Empty;
    public string PrimaryBranchName { get; init; } = string.Empty;
    public bool IsProfileComplete { get; init; }
    public List<CustomerContactDto> Contacts { get; init; } = new();
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
