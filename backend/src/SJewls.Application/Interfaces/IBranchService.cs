using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface IBranchService
{
    Task<List<BranchDto>> GetActiveBranchesAsync(CancellationToken cancellationToken = default);
    Task<List<BranchDto>> GetAllBranchesAsync(string? search = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<BranchDto?> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);
    Task<BranchDto> UpdateBranchAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default);
    Task<BranchDto> UpdateBranchStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task DeleteBranchAsync(Guid id, CancellationToken cancellationToken = default);
}
