using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class JewelleryPlanService : IJewelleryPlanService
{
    private readonly SJewlsDbContext _db;
    private readonly IAuditLogService _auditService;
    private readonly ILogger<JewelleryPlanService> _logger;

    public JewelleryPlanService(
        SJewlsDbContext db,
        IAuditLogService auditService,
        ILogger<JewelleryPlanService> logger)
    {
        _db = db;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<JewelleryPlanPagedResponse> GetPlansAsync(
        Guid staffId,
        string? search,
        Guid? categoryId,
        string? status,
        Guid? branchId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, staffBranchIds) = await GetAuthorizedStaffAsync(staffId, ct);

        var query = _db.JewelleryPlans
            .Include(p => p.Branch)
            .Include(p => p.Category)
            .Include(p => p.Enrolments)
            .AsNoTracking()
            .AsQueryable();

        // Branch scoping
        if (!isSuperAdmin)
        {
            query = query.Where(p => staffBranchIds.Contains(p.BranchId));
            if (branchId.HasValue && !staffBranchIds.Contains(branchId.Value))
            {
                throw new UnauthorizedAccessException("You are not authorized to view plans for this branch.");
            }
            if (branchId.HasValue)
            {
                query = query.Where(p => p.BranchId == branchId.Value);
            }
        }
        else if (branchId.HasValue)
        {
            query = query.Where(p => p.BranchId == branchId.Value);
        }

        // Category filter
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        // Search by plan name
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(searchLower) || p.Code.ToLower().Contains(searchLower));
        }

        var allMatching = await query.ToListAsync(ct);

        // Calculate display status per plan based on its branch timezone
        var evaluatedPlans = allMatching.Select(plan =>
        {
            var branchTz = plan.Branch?.Timezone ?? "Asia/Colombo";
            var branchToday = GetBranchToday(branchTz);
            var displayStatus = DetermineDisplayStatus(plan.IsActive, plan.StartDate, branchToday);

            // Progress formula:
            // total net accumulated grams across enrolments / sum of enrolment target weight snapshots * 100
            var relevantEnrolments = plan.Enrolments
                .Where(e => e.Status != JewelleryEnrolmentStatus.Cancelled)
                .ToList();

            var totalTargetGramsAcrossEnrolments = relevantEnrolments.Sum(e => e.TargetProductWeightGrams);
            var totalNetAccumulatedGrams = relevantEnrolments.Sum(e => e.TotalSavedGrams);
            var activeEnrolmentsCount = relevantEnrolments.Count(e => e.Status == JewelleryEnrolmentStatus.Active);

            decimal overallProgress = 0m;
            if (totalTargetGramsAcrossEnrolments > 0)
            {
                overallProgress = Math.Round((totalNetAccumulatedGrams / totalTargetGramsAcrossEnrolments) * 100m, 1);
                if (overallProgress > 100m) overallProgress = 100m;
            }

            return new
            {
                Plan = plan,
                DisplayStatus = displayStatus,
                TotalEnrolments = relevantEnrolments.Count,
                ActiveEnrolments = activeEnrolmentsCount,
                TotalNetAccumulatedGrams = totalNetAccumulatedGrams,
                TotalTargetGramsAcrossEnrolments = totalTargetGramsAcrossEnrolments,
                OverallProgressPercentage = overallProgress
            };
        }).ToList();

        // Calculate counts for filters
        var activeCount = evaluatedPlans.Count(p => p.DisplayStatus == "Active");
        var scheduledCount = evaluatedPlans.Count(p => p.DisplayStatus == "Scheduled");
        var deactivatedCount = evaluatedPlans.Count(p => p.DisplayStatus == "Deactivated");

        // Status filter
        var filtered = evaluatedPlans;
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = evaluatedPlans.Where(p => p.DisplayStatus.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var totalCount = filtered.Count;
        var pagedItems = filtered
            .OrderByDescending(p => p.Plan.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapToDto(
                x.Plan,
                x.DisplayStatus,
                x.TotalEnrolments,
                x.ActiveEnrolments,
                x.TotalNetAccumulatedGrams,
                x.TotalTargetGramsAcrossEnrolments,
                x.OverallProgressPercentage))
            .ToList();

        return new JewelleryPlanPagedResponse
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            ActiveCount = activeCount,
            ScheduledCount = scheduledCount,
            DeactivatedCount = deactivatedCount
        };
    }

    public async Task<JewelleryPlanDto?> GetPlanByIdAsync(Guid id, Guid staffId, CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, staffBranchIds) = await GetAuthorizedStaffAsync(staffId, ct);

        var plan = await _db.JewelleryPlans
            .Include(p => p.Branch)
            .Include(p => p.Category)
            .Include(p => p.Enrolments)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (plan == null) return null;

        if (!isSuperAdmin && !staffBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to view plans for this branch.");
        }

        var branchToday = GetBranchToday(plan.Branch?.Timezone);
        var displayStatus = DetermineDisplayStatus(plan.IsActive, plan.StartDate, branchToday);

        var relevantEnrolments = plan.Enrolments
            .Where(e => e.Status != JewelleryEnrolmentStatus.Cancelled)
            .ToList();

        var totalTargetGramsAcrossEnrolments = relevantEnrolments.Sum(e => e.TargetProductWeightGrams);
        var totalNetAccumulatedGrams = relevantEnrolments.Sum(e => e.TotalSavedGrams);
        var activeEnrolmentsCount = relevantEnrolments.Count(e => e.Status == JewelleryEnrolmentStatus.Active);

        decimal overallProgress = 0m;
        if (totalTargetGramsAcrossEnrolments > 0)
        {
            overallProgress = Math.Round((totalNetAccumulatedGrams / totalTargetGramsAcrossEnrolments) * 100m, 1);
            if (overallProgress > 100m) overallProgress = 100m;
        }

        return MapToDto(
            plan,
            displayStatus,
            relevantEnrolments.Count,
            activeEnrolmentsCount,
            totalNetAccumulatedGrams,
            totalTargetGramsAcrossEnrolments,
            overallProgress);
    }

    public async Task<JewelleryPlanDto> CreatePlanAsync(
        CreateJewelleryPlanRequest request,
        Guid staffId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, staffBranchIds) = await GetAuthorizedStaffAsync(staffId, ct);

        // 1. Resolve target branch
        Guid targetBranchId;
        if (isSuperAdmin)
        {
            if (request.BranchId.HasValue)
            {
                targetBranchId = request.BranchId.Value;
            }
            else
            {
                var cat = await _db.JewelleryCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);
                if (cat != null)
                {
                    targetBranchId = cat.BranchId;
                }
                else if (staffBranchIds.Any())
                {
                    targetBranchId = staffBranchIds.First();
                }
                else
                {
                    var defaultBranch = await _db.Branches.FirstOrDefaultAsync(b => b.Code == "JAF-01", ct)
                                        ?? await _db.Branches.FirstOrDefaultAsync(ct);
                    targetBranchId = defaultBranch?.Id ?? throw new InvalidOperationException("No branch found.");
                }
            }
        }
        else
        {
            if (request.BranchId.HasValue && !staffBranchIds.Contains(request.BranchId.Value))
            {
                throw new UnauthorizedAccessException("You are not authorized to create plans in this branch.");
            }
            targetBranchId = request.BranchId ?? staffBranchIds.FirstOrDefault();
            if (targetBranchId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Staff member has no assigned branches.");
            }
        }

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == targetBranchId, ct)
                     ?? throw new InvalidOperationException("Branch does not exist.");

        // 2. Validate category
        var category = await _db.JewelleryCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);
        if (category == null)
        {
            throw new ArgumentException("Selected category does not exist.");
        }
        if (category.BranchId != targetBranchId)
        {
            throw new ArgumentException("Selected category belongs to a different branch.");
        }
        if (!category.IsActive)
        {
            throw new ArgumentException("Selected category is currently inactive.");
        }

        // 3. Validate durations
        var validDurations = ValidateAndSanitizeDurations(request.AllowedDurationsMonths);

        // 4. Validate grams
        if (request.TargetProductGoldWeightGrams <= 0)
        {
            throw new ArgumentException("Target gold weight must be a positive number of grams.");
        }

        // 5. Trim and validate name
        var planName = request.Name.Trim();
        if (planName.Length < 2 || planName.Length > 150)
        {
            throw new ArgumentException("Plan name must be between 2 and 150 characters.");
        }

        // 6. Generate unique code
        var code = $"JP-{branch.Code}-{DateTime.UtcNow:yyMMdd}-{RandomNumberGenerator.GetInt32(1000, 9999)}";

        // 7. Create entity
        var plan = new JewelleryPlan
        {
            BranchId = targetBranchId,
            CategoryId = request.CategoryId,
            Code = code,
            Name = planName,
            Description = (request.Description ?? string.Empty).Trim(),
            ImageUrl = request.ImageUrl.Trim(),
            TargetProductGoldWeightGrams = request.TargetProductGoldWeightGrams,
            Karat = 24, // Internal standard 24K
            AllowedDurationsMonths = validDurations,
            StartDate = request.StartDate,
            IsActive = true,
            CancellationFeeType = CancellationFeeType.Percentage,
            CancellationFeeValue = 5.0m,
            TargetMakingCharges = 0m,
            TargetVat = 0m,
            TotalTargetAmount = 0m
        };

        _db.JewelleryPlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        // Audit log
        await _auditService.LogActionAsync(
            "Staff",
            staffId,
            "CreateJewelleryPlan",
            "JewelleryPlan",
            plan.Id.ToString(),
            targetBranchId,
            null,
            new
            {
                plan.Id,
                plan.Code,
                plan.Name,
                plan.CategoryId,
                plan.BranchId,
                plan.TargetProductGoldWeightGrams,
                plan.AllowedDurationsMonths,
                plan.StartDate,
                plan.IsActive
            },
            ipAddress);

        _logger.LogInformation("Jewellery Plan created: {PlanId} ({PlanName}) for branch {BranchCode}",
            plan.Id, plan.Name, branch.Code);

        // Reload with navigations
        plan.Branch = branch;
        plan.Category = category;

        var branchToday = GetBranchToday(branch.Timezone);
        var displayStatus = DetermineDisplayStatus(plan.IsActive, plan.StartDate, branchToday);

        return MapToDto(plan, displayStatus, 0, 0, 0m, 0m, 0m);
    }

    public async Task<JewelleryPlanDto> UpdatePlanAsync(
        Guid id,
        UpdateJewelleryPlanRequest request,
        Guid staffId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, staffBranchIds) = await GetAuthorizedStaffAsync(staffId, ct);

        var plan = await _db.JewelleryPlans
            .Include(p => p.Branch)
            .Include(p => p.Category)
            .Include(p => p.Enrolments)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Jewellery plan with ID '{id}' was not found.");

        if (!isSuperAdmin && !staffBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to update plans in this branch.");
        }

        // Validate category
        var category = await _db.JewelleryCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);
        if (category == null)
        {
            throw new ArgumentException("Selected category does not exist.");
        }
        if (category.BranchId != plan.BranchId)
        {
            throw new ArgumentException("Selected category belongs to a different branch. Cannot move plan between branches.");
        }
        if (!category.IsActive && plan.CategoryId != request.CategoryId)
        {
            throw new ArgumentException("Cannot switch to an inactive category.");
        }

        // Validate durations
        var validDurations = ValidateAndSanitizeDurations(request.AllowedDurationsMonths);

        // Validate grams
        if (request.TargetProductGoldWeightGrams <= 0)
        {
            throw new ArgumentException("Target gold weight must be a positive number of grams.");
        }

        var planName = request.Name.Trim();
        if (planName.Length < 2 || planName.Length > 150)
        {
            throw new ArgumentException("Plan name must be between 2 and 150 characters.");
        }

        var beforeState = new
        {
            plan.Name,
            plan.CategoryId,
            plan.ImageUrl,
            plan.TargetProductGoldWeightGrams,
            plan.AllowedDurationsMonths,
            plan.StartDate,
            plan.Description
        };

        // Note: Existing customer enrolments retain their agreed snapshot terms (target grams, selected duration, start date, deadline).
        // Modifying the plan's values applies to future customer enrolments only.
        plan.Name = planName;
        plan.CategoryId = request.CategoryId;
        plan.Category = category;
        plan.ImageUrl = request.ImageUrl.Trim();
        plan.Description = (request.Description ?? string.Empty).Trim();
        plan.TargetProductGoldWeightGrams = request.TargetProductGoldWeightGrams;
        plan.AllowedDurationsMonths = validDurations;
        plan.StartDate = request.StartDate;

        await _db.SaveChangesAsync(ct);

        // Audit log
        await _auditService.LogActionAsync(
            "Staff",
            staffId,
            "UpdateJewelleryPlan",
            "JewelleryPlan",
            plan.Id.ToString(),
            plan.BranchId,
            beforeState,
            new
            {
                plan.Name,
                plan.CategoryId,
                plan.ImageUrl,
                plan.TargetProductGoldWeightGrams,
                plan.AllowedDurationsMonths,
                plan.StartDate,
                plan.Description
            },
            ipAddress);

        var branchToday = GetBranchToday(plan.Branch?.Timezone);
        var displayStatus = DetermineDisplayStatus(plan.IsActive, plan.StartDate, branchToday);

        var relevantEnrolments = plan.Enrolments
            .Where(e => e.Status != JewelleryEnrolmentStatus.Cancelled)
            .ToList();

        var totalTargetGramsAcrossEnrolments = relevantEnrolments.Sum(e => e.TargetProductWeightGrams);
        var totalNetAccumulatedGrams = relevantEnrolments.Sum(e => e.TotalSavedGrams);
        var activeEnrolmentsCount = relevantEnrolments.Count(e => e.Status == JewelleryEnrolmentStatus.Active);

        decimal overallProgress = 0m;
        if (totalTargetGramsAcrossEnrolments > 0)
        {
            overallProgress = Math.Round((totalNetAccumulatedGrams / totalTargetGramsAcrossEnrolments) * 100m, 1);
            if (overallProgress > 100m) overallProgress = 100m;
        }

        return MapToDto(
            plan,
            displayStatus,
            relevantEnrolments.Count,
            activeEnrolmentsCount,
            totalNetAccumulatedGrams,
            totalTargetGramsAcrossEnrolments,
            overallProgress);
    }

    public async Task<JewelleryPlanDto> UpdatePlanStatusAsync(
        Guid id,
        UpdateJewelleryPlanStatusRequest request,
        Guid staffId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, staffBranchIds) = await GetAuthorizedStaffAsync(staffId, ct);

        var plan = await _db.JewelleryPlans
            .Include(p => p.Branch)
            .Include(p => p.Category)
            .Include(p => p.Enrolments)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Jewellery plan with ID '{id}' was not found.");

        if (!isSuperAdmin && !staffBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to update status for plans in this branch.");
        }

        var action = request.Action?.Trim() ?? string.Empty;
        var beforeState = new { plan.IsActive, plan.DeactivatedAtUtc, plan.ReopenedAtUtc };

        if (action.Equals("Deactivate", StringComparison.OrdinalIgnoreCase))
        {
            if (!plan.IsActive)
            {
                // Already deactivated
                var bToday = GetBranchToday(plan.Branch?.Timezone);
                return MapToDto(plan, "Deactivated", plan.Enrolments.Count, 0, 0, 0, 0);
            }

            plan.IsActive = false;
            plan.DeactivatedAtUtc = DateTimeOffset.UtcNow;
            plan.DeactivatedByStaffId = staffId;

            await _db.SaveChangesAsync(ct);

            await _auditService.LogActionAsync(
                "Staff",
                staffId,
                "DeactivateJewelleryPlan",
                "JewelleryPlan",
                plan.Id.ToString(),
                plan.BranchId,
                beforeState,
                new { plan.IsActive, plan.DeactivatedAtUtc, Reason = request.Reason },
                ipAddress);

            _logger.LogInformation("Jewellery Plan deactivated: {PlanId} by staff {StaffId}", plan.Id, staffId);
        }
        else if (action.Equals("Reopen", StringComparison.OrdinalIgnoreCase))
        {
            if (plan.IsActive)
            {
                // Already active/scheduled
                var bToday = GetBranchToday(plan.Branch?.Timezone);
                var st = DetermineDisplayStatus(plan.IsActive, plan.StartDate, bToday);
                return MapToDto(plan, st, plan.Enrolments.Count, 0, 0, 0, 0);
            }

            plan.IsActive = true;
            plan.ReopenedAtUtc = DateTimeOffset.UtcNow;
            plan.ReopenedByStaffId = staffId;

            await _db.SaveChangesAsync(ct);

            await _auditService.LogActionAsync(
                "Staff",
                staffId,
                "ReopenJewelleryPlan",
                "JewelleryPlan",
                plan.Id.ToString(),
                plan.BranchId,
                beforeState,
                new { plan.IsActive, plan.ReopenedAtUtc, Reason = request.Reason },
                ipAddress);

            _logger.LogInformation("Jewellery Plan reopened: {PlanId} by staff {StaffId}", plan.Id, staffId);
        }
        else
        {
            throw new ArgumentException("Invalid action. Must be 'Deactivate' or 'Reopen'.");
        }

        var branchToday = GetBranchToday(plan.Branch?.Timezone);
        var displayStatus = DetermineDisplayStatus(plan.IsActive, plan.StartDate, branchToday);

        var relevantEnrolments = plan.Enrolments
            .Where(e => e.Status != JewelleryEnrolmentStatus.Cancelled)
            .ToList();

        var totalTargetGramsAcrossEnrolments = relevantEnrolments.Sum(e => e.TargetProductWeightGrams);
        var totalNetAccumulatedGrams = relevantEnrolments.Sum(e => e.TotalSavedGrams);
        var activeEnrolmentsCount = relevantEnrolments.Count(e => e.Status == JewelleryEnrolmentStatus.Active);

        decimal overallProgress = 0m;
        if (totalTargetGramsAcrossEnrolments > 0)
        {
            overallProgress = Math.Round((totalNetAccumulatedGrams / totalTargetGramsAcrossEnrolments) * 100m, 1);
            if (overallProgress > 100m) overallProgress = 100m;
        }

        return MapToDto(
            plan,
            displayStatus,
            relevantEnrolments.Count,
            activeEnrolmentsCount,
            totalNetAccumulatedGrams,
            totalTargetGramsAcrossEnrolments,
            overallProgress);
    }

    public async Task<PlanCustomersPagedResponse> GetPlanCustomersAsync(
        Guid planId,
        Guid staffId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, staffBranchIds) = await GetAuthorizedStaffAsync(staffId, ct);

        var plan = await _db.JewelleryPlans
            .Include(p => p.Branch)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Jewellery plan with ID '{planId}' was not found.");

        if (!isSuperAdmin && !staffBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to view customer enrolments for this plan.");
        }

        var query = _db.JewelleryEnrolments
            .Include(e => e.Customer)
            .Include(e => e.Branch)
            .Include(e => e.Contributions)
                .ThenInclude(c => c.Payment)
            .Where(e => e.JewelleryPlanId == planId)
            .AsNoTracking()
            .AsQueryable();

        // Search by customer name, phone, email
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(e =>
                (e.Customer != null && e.Customer.FullName.ToLower().Contains(searchLower)) ||
                (e.Customer != null && e.Customer.PhoneNumber != null && e.Customer.PhoneNumber.Contains(searchLower)) ||
                (e.Customer != null && e.Customer.Email != null && e.Customer.Email.ToLower().Contains(searchLower)));
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<JewelleryEnrolmentStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(e => e.Status == parsedStatus);
            }
        }

        var allEnrolments = await _db.JewelleryEnrolments
            .Where(e => e.JewelleryPlanId == planId && e.Status != JewelleryEnrolmentStatus.Cancelled)
            .Select(e => new { e.TargetProductWeightGrams, e.TotalSavedGrams })
            .ToListAsync(ct);

        var totalTargetGrams = allEnrolments.Sum(e => e.TargetProductWeightGrams);
        var totalSavedGrams = allEnrolments.Sum(e => e.TotalSavedGrams);
        decimal overallProgress = 0m;
        if (totalTargetGrams > 0)
        {
            overallProgress = Math.Round((totalSavedGrams / totalTargetGrams) * 100m, 1);
            if (overallProgress > 100m) overallProgress = 100m;
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(e =>
        {
            var targetGrams = e.TargetProductWeightGrams;
            var netGrams = e.TotalSavedGrams;
            var remaining = Math.Max(0m, targetGrams - netGrams);

            decimal individualProgress = 0m;
            if (targetGrams > 0)
            {
                individualProgress = Math.Min(100m, Math.Round((netGrams / targetGrams) * 100m, 1));
            }

            var contributions = e.Contributions
                .OrderByDescending(c => c.ConfirmedAtUtc)
                .Select(c => new EnrolmentContributionDto
                {
                    Id = c.Id,
                    CurrencyAmount = c.CurrencyAmount,
                    Currency = e.Branch?.Currency ?? "LKR",
                    GoldGrams = c.GoldGrams,
                    AppliedRatePerGram = c.AppliedRatePerGram,
                    Karat = c.Karat,
                    ConfirmedAtUtc = c.ConfirmedAtUtc,
                    Status = c.Payment?.Status.ToString() ?? "Succeeded",
                    PaymentMethod = c.Payment?.Method.ToString() ?? "Online"
                })
                .ToList();

            return new PlanCustomerEnrolmentDto
            {
                EnrolmentId = e.Id,
                CustomerId = e.CustomerId,
                CustomerName = e.Customer?.FullName ?? "Unknown Customer",
                PhoneNumber = e.Customer?.PhoneNumber ?? string.Empty,
                Email = e.Customer?.Email ?? string.Empty,
                JoiningDate = e.StartDate,
                SelectedDurationMonths = e.SelectedDurationMonths,
                Deadline = e.TargetEndDate,
                EnrolmentTargetGrams = targetGrams,
                TotalConfirmedMoneyPaid = e.TotalContributedCurrency,
                Currency = e.Branch?.Currency ?? "LKR",
                NetAccumulatedGrams = netGrams,
                RemainingGrams = remaining,
                IndividualProgressPercentage = individualProgress,
                EnrolmentStatus = e.Status.ToString(),
                Contributions = contributions
            };
        }).ToList();

        return new PlanCustomersPagedResponse
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            CategoryName = plan.Category?.Name ?? string.Empty,
            PlanTargetGrams = plan.TargetProductGoldWeightGrams,
            Currency = plan.Branch?.Currency ?? "LKR",
            OverallProgressPercentage = overallProgress,
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FinancialQuoteResponse> CalculateFinancialQuoteAsync(
        FinancialQuoteRequest request,
        Guid staffId,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, staffBranchIds) = await GetAuthorizedStaffAsync(staffId, ct);

        if (!isSuperAdmin && !staffBranchIds.Contains(request.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to access rates for this branch.");
        }

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, ct)
                     ?? throw new KeyNotFoundException("Branch not found.");

        // Query current applicable 24K gold rate
        var currentRate = await _db.GoldRates
            .Where(r => r.BranchId == request.BranchId && r.Karat == 24 && r.EffectiveFromUtc <= DateTimeOffset.UtcNow)
            .OrderByDescending(r => r.EffectiveFromUtc)
            .FirstOrDefaultAsync(ct);

        if (currentRate == null || currentRate.RatePerGram <= 0)
        {
            throw new InvalidOperationException("No valid applicable 24K gold rate is currently configured for this branch.");
        }

        decimal payableMoney = 0m;
        decimal creditedGrams = 0m;
        decimal? remainingTargetGrams = null;
        bool willExceedTarget = false;

        // Check enrolment remaining balance if EnrolmentId provided
        if (request.EnrolmentId.HasValue)
        {
            var enrolment = await _db.JewelleryEnrolments.FirstOrDefaultAsync(e => e.Id == request.EnrolmentId.Value, ct);
            if (enrolment != null)
            {
                remainingTargetGrams = Math.Max(0m, enrolment.TargetProductWeightGrams - enrolment.TotalSavedGrams);
            }
        }

        var mode = request.Mode?.Trim() ?? "ByGrams";

        if (mode.Equals("ByGrams", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.InputGrams.HasValue || request.InputGrams.Value <= 0)
            {
                throw new ArgumentException("Input grams must be a positive decimal.");
            }

            var inputGrams = request.InputGrams.Value;
            creditedGrams = inputGrams;
            // Round payable money to 2 decimal places
            payableMoney = Math.Round(inputGrams * currentRate.RatePerGram, 2, MidpointRounding.AwayFromZero);

            if (remainingTargetGrams.HasValue && creditedGrams > remainingTargetGrams.Value)
            {
                willExceedTarget = true;
            }
        }
        else if (mode.Equals("ByMoney", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.InputMoney.HasValue || request.InputMoney.Value <= 0)
            {
                throw new ArgumentException("Input money must be a positive decimal.");
            }

            var inputMoney = request.InputMoney.Value;
            payableMoney = inputMoney;
            // Round credited grams down to 4 decimal places so credited grams never round up above payment
            creditedGrams = Math.Round(inputMoney / currentRate.RatePerGram, 4, MidpointRounding.ToZero);

            if (remainingTargetGrams.HasValue)
            {
                if (creditedGrams > remainingTargetGrams.Value)
                {
                    willExceedTarget = true;
                    // Do not round credited grams above remaining balance
                    creditedGrams = remainingTargetGrams.Value;
                }
            }
        }
        else
        {
            throw new ArgumentException("Invalid mode. Allowed modes are 'ByGrams' or 'ByMoney'.");
        }

        return new FinancialQuoteResponse
        {
            BranchId = branch.Id,
            RatePerGram = currentRate.RatePerGram,
            EffectiveFromUtc = currentRate.EffectiveFromUtc,
            Currency = branch.Currency,
            PayableMoney = payableMoney,
            CreditedGrams = creditedGrams,
            RemainingTargetGrams = remainingTargetGrams,
            WillExceedTarget = willExceedTarget,
            QuoteExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(15)
        };
    }

    #region Helper Methods

    private async Task<(Staff staff, bool isSuperAdmin, List<Guid> staffBranchIds)> GetAuthorizedStaffAsync(Guid staffId, CancellationToken ct)
    {
        var staff = await _db.StaffMembers
            .Include(s => s.Roles).ThenInclude(sr => sr.Role)
            .Include(s => s.AssignedBranches).ThenInclude(sb => sb.Branch)
            .FirstOrDefaultAsync(s => s.Id == staffId, ct);

        if (staff == null || !staff.IsActive)
        {
            throw new UnauthorizedAccessException("Staff user account is inactive or not found.");
        }

        var isSuperAdmin = staff.Roles.Any(r => r.Role != null &&
            (r.Role.Name.Equals("Super Admin", StringComparison.OrdinalIgnoreCase) ||
             r.Role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)));

        var staffBranchIds = staff.AssignedBranches.Select(b => b.BranchId).ToList();

        return (staff, isSuperAdmin, staffBranchIds);
    }

    private static int[] ValidateAndSanitizeDurations(IEnumerable<int>? durations)
    {
        if (durations == null)
        {
            throw new ArgumentException("At least one duration option must be selected.");
        }

        var valid = durations
            .Where(d => d > 0)
            .Distinct()
            .OrderBy(d => d)
            .ToArray();

        if (valid.Length == 0)
        {
            throw new ArgumentException("At least one positive whole month duration option must be selected.");
        }

        return valid;
    }

    private static DateOnly GetBranchToday(string? timezoneStr)
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneStr ?? "Asia/Colombo");
        }
        catch
        {
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time");
            }
            catch
            {
                tz = TimeZoneInfo.Utc;
            }
        }

        var branchNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        return DateOnly.FromDateTime(branchNow.DateTime);
    }

    private static string DetermineDisplayStatus(bool isActive, DateOnly startDate, DateOnly branchToday)
    {
        if (!isActive)
        {
            return "Deactivated";
        }

        if (startDate > branchToday)
        {
            return "Scheduled";
        }

        return "Active";
    }

    private static JewelleryPlanDto MapToDto(
        JewelleryPlan plan,
        string displayStatus,
        int totalEnrolments,
        int activeEnrolments,
        decimal totalNetAccumulatedGrams,
        decimal totalTargetGramsAcrossEnrolments,
        decimal overallProgressPercentage)
    {
        return new JewelleryPlanDto
        {
            Id = plan.Id,
            BranchId = plan.BranchId,
            BranchName = plan.Branch?.Name ?? string.Empty,
            BranchCode = plan.Branch?.Code ?? string.Empty,
            Currency = plan.Branch?.Currency ?? "LKR",
            Code = plan.Code,
            Name = plan.Name,
            Description = plan.Description,
            ImageUrl = plan.ImageUrl,
            CategoryId = plan.CategoryId,
            CategoryName = plan.Category?.Name ?? string.Empty,
            TargetProductGoldWeightGrams = plan.TargetProductGoldWeightGrams,
            Karat = plan.Karat,
            AllowedDurationsMonths = plan.AllowedDurationsMonths,
            StartDate = plan.StartDate,
            IsActive = plan.IsActive,
            DisplayStatus = displayStatus,
            TotalEnrolments = totalEnrolments,
            ActiveEnrolments = activeEnrolments,
            TotalNetAccumulatedGrams = totalNetAccumulatedGrams,
            TotalTargetGramsAcrossEnrolments = totalTargetGramsAcrossEnrolments,
            OverallProgressPercentage = overallProgressPercentage,
            CreatedAtUtc = plan.CreatedAtUtc,
            UpdatedAtUtc = plan.UpdatedAtUtc,
            DeactivatedAtUtc = plan.DeactivatedAtUtc,
            ReopenedAtUtc = plan.ReopenedAtUtc
        };
    }

    #endregion
}
