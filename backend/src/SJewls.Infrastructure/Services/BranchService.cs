using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class BranchService : IBranchService
{
    private readonly SJewlsDbContext _db;
    private readonly ILogger<BranchService> _logger;

    public BranchService(SJewlsDbContext db, ILogger<BranchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<BranchDto>> GetActiveBranchesAsync(CancellationToken cancellationToken = default)
    {
        var branches = await _db.Branches
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Code)
            .ToListAsync(cancellationToken);

        return branches.Select(b => MapToDto(b)).ToList();
    }

    public async Task<List<BranchDto>> GetAllBranchesAsync(string? search = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Branches.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(b => b.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(b =>
                b.Code.ToLower().Contains(term) ||
                b.Name.ToLower().Contains(term) ||
                b.City.ToLower().Contains(term) ||
                b.Address.ToLower().Contains(term));
        }

        var branches = await query.OrderBy(b => b.Code).ToListAsync(cancellationToken);

        // Fetch counts in aggregate
        var branchIds = branches.Select(b => b.Id).ToList();

        var staffCounts = await _db.StaffBranches
            .AsNoTracking()
            .Where(sb => branchIds.Contains(sb.BranchId))
            .GroupBy(sb => sb.BranchId)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, cancellationToken);

        var customerCounts = await _db.Customers
            .AsNoTracking()
            .Where(c => branchIds.Contains(c.PrimaryBranchId))
            .GroupBy(c => c.PrimaryBranchId)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, cancellationToken);

        return branches.Select(b => MapToDto(
            b,
            staffCounts.TryGetValue(b.Id, out var sc) ? sc : 0,
            customerCounts.TryGetValue(b.Id, out var cc) ? cc : 0
        )).ToList();
    }

    public async Task<BranchDto?> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var branch = await _db.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (branch == null) return null;

        var staffCount = await _db.StaffBranches
            .CountAsync(sb => sb.BranchId == id, cancellationToken);

        var customerCount = await _db.Customers
            .CountAsync(c => c.PrimaryBranchId == id, cancellationToken);

        return MapToDto(branch, staffCount, customerCount);
    }

    public async Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("Branch code is required.", nameof(request.Code));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Branch name is required.", nameof(request.Name));

        if (string.IsNullOrWhiteSpace(request.Address))
            throw new ArgumentException("Branch address is required.", nameof(request.Address));

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var codeExists = await _db.Branches
            .AnyAsync(b => b.Code.ToUpper() == normalizedCode, cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException($"A branch with code '{normalizedCode}' already exists.");
        }

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            City = string.IsNullOrWhiteSpace(request.City) ? "Jaffna" : request.City.Trim(),
            Country = string.IsNullOrWhiteSpace(request.Country) ? "Sri Lanka" : request.Country.Trim(),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "LKR" : request.Currency.Trim().ToUpperInvariant(),
            Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "Asia/Colombo" : request.Timezone.Trim(),
            IsActive = request.IsActive,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = null
        };

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new branch '{BranchName}' ({BranchCode}, ID: {BranchId})", branch.Name, branch.Code, branch.Id);

        return MapToDto(branch);
    }

    public async Task<BranchDto> UpdateBranchAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null)
        {
            throw new KeyNotFoundException($"Branch with ID '{id}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Branch name is required.", nameof(request.Name));

        if (string.IsNullOrWhiteSpace(request.Address))
            throw new ArgumentException("Branch address is required.", nameof(request.Address));

        branch.Name = request.Name.Trim();
        branch.Address = request.Address.Trim();
        branch.City = string.IsNullOrWhiteSpace(request.City) ? branch.City : request.City.Trim();
        branch.Country = string.IsNullOrWhiteSpace(request.Country) ? branch.Country : request.Country.Trim();
        branch.Currency = string.IsNullOrWhiteSpace(request.Currency) ? branch.Currency : request.Currency.Trim().ToUpperInvariant();
        branch.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? branch.Timezone : request.Timezone.Trim();
        branch.IsActive = request.IsActive;
        branch.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated branch '{BranchName}' ({BranchCode}, ID: {BranchId})", branch.Name, branch.Code, branch.Id);

        return (await GetBranchByIdAsync(id, cancellationToken))!;
    }

    public async Task<BranchDto> UpdateBranchStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null)
        {
            throw new KeyNotFoundException($"Branch with ID '{id}' was not found.");
        }

        if (!isActive)
        {
            // Safety check: Don't allow deactivating the only active branch
            var activeCount = await _db.Branches.CountAsync(b => b.IsActive && b.Id != id, cancellationToken);
            if (activeCount == 0)
            {
                throw new InvalidOperationException("Cannot deactivate the only active branch in the system.");
            }
        }

        branch.IsActive = isActive;
        branch.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated branch status for '{BranchName}' ({BranchCode}) to IsActive={IsActive}", branch.Name, branch.Code, isActive);

        return (await GetBranchByIdAsync(id, cancellationToken))!;
    }

    public async Task DeleteBranchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null)
        {
            throw new KeyNotFoundException($"Branch with ID '{id}' was not found.");
        }

        // Safety check 1: Active branch count
        var activeCount = await _db.Branches.CountAsync(b => b.IsActive && b.Id != id, cancellationToken);
        if (branch.IsActive && activeCount == 0)
        {
            throw new InvalidOperationException("Cannot delete the only remaining active branch in the system.");
        }

        // Safety check 2: Check assigned customers
        var customerCount = await _db.Customers.CountAsync(c => c.PrimaryBranchId == id, cancellationToken);
        if (customerCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete branch '{branch.Name}' ({branch.Code}) because {customerCount} customer(s) are assigned to it. Please deactivate the branch instead.");
        }

        // Safety check 3: Check assigned staff
        var staffCount = await _db.StaffBranches.CountAsync(sb => sb.BranchId == id, cancellationToken);
        if (staffCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete branch '{branch.Name}' ({branch.Code}) because {staffCount} staff member(s) are assigned to it. Please reassign the staff or deactivate the branch instead.");
        }

        // Safety check 4: Check linked plans
        var chituPlansCount = await _db.ChituPlans.CountAsync(cp => cp.BranchId == id, cancellationToken);
        var jewelleryPlansCount = await _db.JewelleryPlans.CountAsync(jp => jp.BranchId == id, cancellationToken);
        if (chituPlansCount > 0 || jewelleryPlansCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete branch '{branch.Name}' ({branch.Code}) because active savings or jewellery plans are linked to it. Please deactivate the branch instead.");
        }

        _db.Branches.Remove(branch);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted branch '{BranchName}' ({BranchCode}, ID: {BranchId})", branch.Name, branch.Code, id);
    }

    private static BranchDto MapToDto(Branch branch, int staffCount = 0, int customerCount = 0)
    {
        return new BranchDto
        {
            Id = branch.Id,
            Code = branch.Code,
            Name = branch.Name,
            Address = branch.Address,
            City = branch.City,
            Country = branch.Country,
            Currency = branch.Currency,
            Timezone = branch.Timezone,
            IsActive = branch.IsActive,
            CreatedAtUtc = branch.CreatedAtUtc,
            UpdatedAtUtc = branch.UpdatedAtUtc,
            AssignedStaffCount = staffCount,
            CustomerCount = customerCount
        };
    }
}
