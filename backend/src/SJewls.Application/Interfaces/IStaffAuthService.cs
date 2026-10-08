using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface IStaffAuthService
{
    Task SeedInitialSuperAdminAsync(CancellationToken cancellationToken = default);
    Task<StaffLoginResponse> LoginAsync(StaffLoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<StaffUserDto> GetCurrentStaffAsync(Guid staffId, string? securityStamp, CancellationToken cancellationToken = default);
    Task LogoutAsync(Guid staffId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, string? originUrl, string? ipAddress, CancellationToken cancellationToken = default);
    Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<StaffUserDto> CreateStaffUserAsync(CreateStaffUserRequest request, Guid currentStaffId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<List<StaffUserDto>> GetStaffUsersAsync(Guid currentStaffId, CancellationToken cancellationToken = default);
    Task<StaffUserDto> UpdateStaffStatusAsync(Guid currentStaffId, Guid targetStaffId, UpdateStaffStatusRequest request, string? ipAddress, CancellationToken cancellationToken = default);
}
