using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class JewelleryCategoryService : IJewelleryCategoryService
{
    private readonly SJewlsDbContext _db;
    private readonly IAuditLogService _auditService;
    private readonly ILogger<JewelleryCategoryService> _logger;

    public JewelleryCategoryService(
        SJewlsDbContext db,
        IAuditLogService auditService,
        ILogger<JewelleryCategoryService> logger)
    {
        _db = db;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<JewelleryCategoryPagedResponse> GetCategoriesAsync(
        Guid currentStaffId,
        string? search = null,
        bool? isActive = null,
        Guid? branchId = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, cancellationToken);

        var query = _db.JewelleryCategories
            .AsNoTracking()
            .Include(c => c.Branch)
            .Include(c => c.JewelleryPlans)
            .AsQueryable();

        // 1. Branch scoping
        if (!isSuperAdmin)
        {
            query = query.Where(c => permittedBranchIds.Contains(c.BranchId));
        }
        else if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            query = query.Where(c => c.BranchId == branchId.Value);
        }

        // 2. Active status filter
        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        // 3. Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.Description != null && c.Description.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var categories = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = categories.Select(c => MapToDto(c, c.JewelleryPlans.Count)).ToList();

        return new JewelleryCategoryPagedResponse
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<JewelleryCategoryDto?> GetCategoryByIdAsync(
        Guid id,
        Guid currentStaffId,
        CancellationToken cancellationToken = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, cancellationToken);

        var category = await _db.JewelleryCategories
            .AsNoTracking()
            .Include(c => c.Branch)
            .Include(c => c.JewelleryPlans)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null) return null;

        if (!isSuperAdmin && !permittedBranchIds.Contains(category.BranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission to view categories in this branch.");
        }

        return MapToDto(category, category.JewelleryPlans.Count);
    }

    public async Task<JewelleryCategoryDto> CreateCategoryAsync(
        CreateJewelleryCategoryRequest request,
        Guid currentStaffId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Category name is required.", nameof(request.Name));
        }

        // Determine target branch
        Guid targetBranchId;
        if (isSuperAdmin)
        {
            if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
            {
                var branchExists = await _db.Branches.AnyAsync(b => b.Id == request.BranchId.Value, cancellationToken);
                if (!branchExists)
                    throw new ArgumentException($"Branch with ID '{request.BranchId.Value}' was not found.");
                targetBranchId = request.BranchId.Value;
            }
            else
            {
                // Default to first active branch
                var defaultBranch = await _db.Branches.FirstOrDefaultAsync(b => b.IsActive, cancellationToken)
                    ?? await _db.Branches.FirstAsync(cancellationToken);
                targetBranchId = defaultBranch.Id;
            }
        }
        else
        {
            if (request.BranchId.HasValue && !permittedBranchIds.Contains(request.BranchId.Value))
            {
                throw new UnauthorizedAccessException("You do not have access to create categories for this branch.");
            }
            targetBranchId = request.BranchId ?? permittedBranchIds.FirstOrDefault();
            if (targetBranchId == Guid.Empty)
            {
                throw new InvalidOperationException("Your staff account is not assigned to any active branch.");
            }
        }

        var normalizedName = request.Name.Trim().ToUpperInvariant();

        // Check for duplicate normalized name in the same branch
        var duplicateExists = await _db.JewelleryCategories
            .AnyAsync(c => c.BranchId == targetBranchId && c.NormalizedName == normalizedName, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException($"A jewellery category named '{request.Name.Trim()}' already exists for this branch.");
        }

        var category = new JewelleryPlanCategory
        {
            Id = Guid.NewGuid(),
            BranchId = targetBranchId,
            Name = request.Name.Trim(),
            NormalizedName = normalizedName,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            IsActive = request.IsActive,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = null,
        };

        _db.JewelleryCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        // Audit Log
        await _auditService.LogActionAsync(
            actorType: "Staff",
            actorId: currentStaffId,
            action: "CreateJewelleryCategory",
            targetEntity: "JewelleryPlanCategory",
            targetId: category.Id.ToString(),
            branchId: category.BranchId,
            before: null,
            after: new { category.Name, category.BranchId, category.IsActive, category.ImageUrl },
            ipAddress: ipAddress);

        _logger.LogInformation("Staff {StaffId} created Jewellery Category '{CategoryName}' (ID: {CategoryId}) for Branch {BranchId}",
            currentStaffId, category.Name, category.Id, category.BranchId);

        return (await GetCategoryByIdAsync(category.Id, currentStaffId, cancellationToken))!;
    }

    public async Task<JewelleryCategoryDto> UpdateCategoryAsync(
        Guid id,
        UpdateJewelleryCategoryRequest request,
        Guid currentStaffId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, cancellationToken);

        var category = await _db.JewelleryCategories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
        {
            throw new KeyNotFoundException($"Jewellery category with ID '{id}' was not found.");
        }

        if (!isSuperAdmin && !permittedBranchIds.Contains(category.BranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission to modify categories in this branch.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Category name is required.", nameof(request.Name));
        }

        var normalizedName = request.Name.Trim().ToUpperInvariant();

        // Check for duplicate normalized name in the same branch
        var duplicateExists = await _db.JewelleryCategories
            .AnyAsync(c => c.BranchId == category.BranchId && c.NormalizedName == normalizedName && c.Id != id, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException($"A jewellery category named '{request.Name.Trim()}' already exists for this branch.");
        }

        var beforeSnapshot = new { category.Name, category.Description, category.ImageUrl, category.IsActive };

        category.Name = request.Name.Trim();
        category.NormalizedName = normalizedName;
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Audit Log
        await _auditService.LogActionAsync(
            actorType: "Staff",
            actorId: currentStaffId,
            action: "UpdateJewelleryCategory",
            targetEntity: "JewelleryPlanCategory",
            targetId: category.Id.ToString(),
            branchId: category.BranchId,
            before: beforeSnapshot,
            after: new { category.Name, category.Description, category.ImageUrl, category.IsActive },
            ipAddress: ipAddress);

        _logger.LogInformation("Staff {StaffId} updated Jewellery Category '{CategoryName}' (ID: {CategoryId})",
            currentStaffId, category.Name, category.Id);

        return (await GetCategoryByIdAsync(category.Id, currentStaffId, cancellationToken))!;
    }

    public async Task<JewelleryCategoryDto> UpdateCategoryStatusAsync(
        Guid id,
        bool isActive,
        Guid currentStaffId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, cancellationToken);

        var category = await _db.JewelleryCategories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
        {
            throw new KeyNotFoundException($"Jewellery category with ID '{id}' was not found.");
        }

        if (!isSuperAdmin && !permittedBranchIds.Contains(category.BranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission to modify categories in this branch.");
        }

        var beforeStatus = category.IsActive;
        category.IsActive = isActive;
        category.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Audit Log
        await _auditService.LogActionAsync(
            actorType: "Staff",
            actorId: currentStaffId,
            action: "UpdateJewelleryCategoryStatus",
            targetEntity: "JewelleryPlanCategory",
            targetId: category.Id.ToString(),
            branchId: category.BranchId,
            before: new { isActive = beforeStatus },
            after: new { isActive },
            ipAddress: ipAddress);

        _logger.LogInformation("Staff {StaffId} updated Jewellery Category '{CategoryName}' status to {IsActive}",
            currentStaffId, category.Name, isActive);

        return (await GetCategoryByIdAsync(category.Id, currentStaffId, cancellationToken))!;
    }

    public async Task DeleteCategoryAsync(
        Guid id,
        Guid currentStaffId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, cancellationToken);

        var category = await _db.JewelleryCategories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
        {
            throw new KeyNotFoundException($"Jewellery category with ID '{id}' was not found.");
        }

        if (!isSuperAdmin && !permittedBranchIds.Contains(category.BranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission to delete categories in this branch.");
        }

        // Safety Rule: Prevent deletion of categories with associated plans
        var associatedPlanCount = await _db.JewelleryPlans
            .CountAsync(p => p.CategoryId == id, cancellationToken);

        if (associatedPlanCount > 0)
        {
            throw new InvalidOperationException(
                $"Cannot delete category '{category.Name}' because it has {associatedPlanCount} associated jewellery plan(s). Please deactivate the category instead.");
        }

        _db.JewelleryCategories.Remove(category);
        await _db.SaveChangesAsync(cancellationToken);

        // Audit Log
        await _auditService.LogActionAsync(
            actorType: "Staff",
            actorId: currentStaffId,
            action: "DeleteJewelleryCategory",
            targetEntity: "JewelleryPlanCategory",
            targetId: category.Id.ToString(),
            branchId: category.BranchId,
            before: new { category.Name, category.BranchId },
            after: null,
            ipAddress: ipAddress);

        _logger.LogInformation("Staff {StaffId} deleted unused Jewellery Category '{CategoryName}' (ID: {CategoryId})",
            currentStaffId, category.Name, id);
    }

    public async Task<List<JewelleryCategoryDto>> GetActiveCategoriesForCustomerBrowsingAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var categories = await _db.JewelleryCategories
            .AsNoTracking()
            .Include(c => c.Branch)
            .Include(c => c.JewelleryPlans.Where(p => p.IsActive))
            .Where(c => c.BranchId == branchId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return categories.Select(c => MapToDto(c, c.JewelleryPlans.Count)).ToList();
    }

    private async Task<(Staff caller, bool isSuperAdmin, List<Guid> permittedBranchIds)> VerifyStaffAccessAsync(
        Guid staffId,
        CancellationToken cancellationToken)
    {
        var staff = await _db.StaffMembers
            .AsNoTracking()
            .Include(s => s.AssignedBranches)
            .Include(s => s.Roles).ThenInclude(sr => sr.Role)
            .FirstOrDefaultAsync(s => s.Id == staffId, cancellationToken);

        if (staff == null || !staff.IsActive)
        {
            throw new UnauthorizedAccessException("Staff account is deactivated or not found.");
        }

        var isSuperAdmin = staff.Roles.Any(sr =>
            sr.Role != null && sr.Role.Name.Equals("Super Admin", StringComparison.OrdinalIgnoreCase));

        var permittedBranchIds = staff.AssignedBranches.Select(b => b.BranchId).ToList();

        return (staff, isSuperAdmin, permittedBranchIds);
    }

    private static JewelleryCategoryDto MapToDto(JewelleryPlanCategory category, int planCount)
    {
        return new JewelleryCategoryDto
        {
            Id = category.Id,
            BranchId = category.BranchId,
            BranchName = category.Branch?.Name ?? string.Empty,
            BranchCode = category.Branch?.Code ?? string.Empty,
            Name = category.Name,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            IsActive = category.IsActive,
            PlanCount = planCount,
            CreatedAtUtc = category.CreatedAtUtc,
            UpdatedAtUtc = category.UpdatedAtUtc,
        };
    }
}
