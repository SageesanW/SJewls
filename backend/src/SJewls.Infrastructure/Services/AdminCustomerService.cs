using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SJewls.Application.Common;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class AdminCustomerService : IAdminCustomerService
{
    private readonly SJewlsDbContext _db;
    private readonly IAuditLogService _auditService;
    private readonly ILogger<AdminCustomerService> _logger;

    public AdminCustomerService(
        SJewlsDbContext db,
        IAuditLogService auditService,
        ILogger<AdminCustomerService> logger)
    {
        _db = db;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<AdminCustomerPagedResponse> GetCustomersAsync(
        Guid currentStaffId,
        int page = 1,
        int pageSize = 10,
        string? search = null,
        Guid? branchId = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, cancellationToken);

        IQueryable<Customer> query = _db.Customers
            .AsNoTracking()
            .Include(c => c.PrimaryBranch)
            .Include(c => c.ChituSlots)
            .Include(c => c.JewelleryEnrolments);

        // Branch-level enforcement
        if (isSuperAdmin)
        {
            if (branchId.HasValue && branchId.Value != Guid.Empty)
            {
                query = query.Where(c => c.PrimaryBranchId == branchId.Value);
            }
        }
        else
        {
            if (branchId.HasValue && branchId.Value != Guid.Empty)
            {
                if (!permittedBranchIds.Contains(branchId.Value))
                {
                    throw new UnauthorizedAccessException("You do not have permission to view customers from this branch.");
                }
                query = query.Where(c => c.PrimaryBranchId == branchId.Value);
            }
            else
            {
                query = query.Where(c => permittedBranchIds.Contains(c.PrimaryBranchId));
            }
        }

        // Search filter (Name, Phone, Email, NIC)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(term)) ||
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                (c.Nic != null && c.Nic.ToLower().Contains(term)));
        }

        // Status filter (Use customer account status, not contact verification or plan activity, to determine active/inactive)
        if (!string.IsNullOrWhiteSpace(status))
        {
            switch (status.Trim().ToLowerInvariant())
            {
                case "active":
                    query = query.Where(c => c.IsActive);
                    break;
                case "inactive":
                    query = query.Where(c => !c.IsActive);
                    break;
                case "pending":
                    query = query.Where(c => !c.IsProfileComplete || (!c.IsPhoneVerified && !c.IsEmailVerified));
                    break;
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var customers = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = customers.Select(c =>
        {
            var activeChitu = c.ChituSlots.Count(s => s.Status == ChituSlotStatus.Active);
            var activeJewellery = c.JewelleryEnrolments.Count(e => e.Status == JewelleryEnrolmentStatus.Active);
            var totalSlots = c.ChituSlots.Count + c.JewelleryEnrolments.Count;

            return new AdminCustomerListItemDto
            {
                Id = c.Id,
                FullName = c.FullName,
                PhoneNumber = c.PhoneNumber,
                Email = c.Email,
                Nic = c.Nic,
                IsPhoneVerified = c.IsPhoneVerified,
                IsEmailVerified = c.IsEmailVerified,
                PrimaryBranchId = c.PrimaryBranchId,
                PrimaryBranchName = c.PrimaryBranch?.Name ?? "Main Branch",
                PrimaryBranchCode = c.PrimaryBranch?.Code ?? "JAF-01",
                IsActive = c.IsActive,
                IsProfileComplete = c.IsProfileComplete,
                ActivePlansCount = activeChitu + activeJewellery,
                TotalSlotsCount = totalSlots,
                CreatedAtUtc = c.CreatedAtUtc,
                DeactivatedAtUtc = c.DeactivatedAtUtc,
                DeactivationReason = c.DeactivationReason,
                ClosedAtUtc = c.ClosedAtUtc,
                ClosureReason = c.ClosureReason
            };
        }).ToList();

        return new AdminCustomerPagedResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AdminCustomerMetricsDto> GetCustomerMetricsAsync(
        Guid currentStaffId,
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, cancellationToken);

        IQueryable<Customer> customerQuery = _db.Customers.AsNoTracking();
        IQueryable<ChituSlot> slotQuery = _db.ChituSlots.AsNoTracking();
        IQueryable<JewelleryEnrolment> enrolmentQuery = _db.JewelleryEnrolments.AsNoTracking();

        if (isSuperAdmin)
        {
            if (branchId.HasValue && branchId.Value != Guid.Empty)
            {
                customerQuery = customerQuery.Where(c => c.PrimaryBranchId == branchId.Value);
                slotQuery = slotQuery.Where(s => s.Customer != null && s.Customer.PrimaryBranchId == branchId.Value);
                enrolmentQuery = enrolmentQuery.Where(e => e.BranchId == branchId.Value);
            }
        }
        else
        {
            if (branchId.HasValue && branchId.Value != Guid.Empty)
            {
                if (!permittedBranchIds.Contains(branchId.Value))
                {
                    throw new UnauthorizedAccessException("You do not have permission to view metrics for this branch.");
                }
                customerQuery = customerQuery.Where(c => c.PrimaryBranchId == branchId.Value);
                slotQuery = slotQuery.Where(s => s.Customer != null && s.Customer.PrimaryBranchId == branchId.Value);
                enrolmentQuery = enrolmentQuery.Where(e => e.BranchId == branchId.Value);
            }
            else
            {
                customerQuery = customerQuery.Where(c => permittedBranchIds.Contains(c.PrimaryBranchId));
                slotQuery = slotQuery.Where(s => s.Customer != null && permittedBranchIds.Contains(s.Customer.PrimaryBranchId));
                enrolmentQuery = enrolmentQuery.Where(e => permittedBranchIds.Contains(e.BranchId));
            }
        }

        var totalCustomers = await customerQuery.CountAsync(cancellationToken);
        var totalSlots = (await slotQuery.CountAsync(cancellationToken)) + (await enrolmentQuery.CountAsync(cancellationToken));
        
        var activeInvestment = await customerQuery.CountAsync(c =>
            c.ChituSlots.Any(s => s.Status == ChituSlotStatus.Active) ||
            c.JewelleryEnrolments.Any(e => e.Status == JewelleryEnrolmentStatus.Active), cancellationToken);

        var closedAccounts = await customerQuery.CountAsync(c =>
            c.ChituSlots.Any(s => s.Status == ChituSlotStatus.Closed || s.Status == ChituSlotStatus.Cancelled) ||
            c.JewelleryEnrolments.Any(e => e.Status == JewelleryEnrolmentStatus.Completed || e.Status == JewelleryEnrolmentStatus.Cancelled), cancellationToken);

        var inactiveCustomers = await customerQuery.CountAsync(c => !c.IsActive, cancellationToken);

        var pendingAccounts = await customerQuery.CountAsync(c =>
            !c.IsProfileComplete || (!c.IsPhoneVerified && !c.IsEmailVerified), cancellationToken);

        return new AdminCustomerMetricsDto
        {
            TotalSlots = totalSlots,
            TotalCustomers = totalCustomers,
            ActiveInvestment = activeInvestment,
            ClosedAccounts = closedAccounts,
            InactiveCustomers = inactiveCustomers,
            PendingAccounts = pendingAccounts
        };
    }

    public async Task<AdminCustomerDetailDto> GetCustomerByIdAsync(
        Guid currentStaffId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, cancellationToken);

        var customer = await _db.Customers
            .AsNoTracking()
            .Include(c => c.PrimaryBranch)
            .Include(c => c.ChituSlots)
                .ThenInclude(s => s.ChituPlan)
            .Include(c => c.JewelleryEnrolments)
                .ThenInclude(e => e.JewelleryPlan)
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer == null)
        {
            throw new KeyNotFoundException($"Customer with ID {customerId} was not found.");
        }

        // Enforce branch permission
        if (!isSuperAdmin && !permittedBranchIds.Contains(customer.PrimaryBranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission to view details for this customer.");
        }

        return new AdminCustomerDetailDto
        {
            Id = customer.Id,
            FullName = customer.FullName,
            DateOfBirth = customer.DateOfBirth,
            Nic = customer.Nic,
            PhoneNumber = customer.PhoneNumber,

            Email = customer.Email,
            IsPhoneVerified = customer.IsPhoneVerified,
            IsEmailVerified = customer.IsEmailVerified,
            PhoneVerifiedAtUtc = customer.PhoneVerifiedAtUtc,
            EmailVerifiedAtUtc = customer.EmailVerifiedAtUtc,
            PrimaryBranchId = customer.PrimaryBranchId,
            PrimaryBranchName = customer.PrimaryBranch?.Name ?? "Main Branch",
            PrimaryBranchCode = customer.PrimaryBranch?.Code ?? "JAF-01",
            IsActive = customer.IsActive,
            IsProfileComplete = customer.IsProfileComplete,
            CreatedAtUtc = customer.CreatedAtUtc,
            DeactivatedAtUtc = customer.DeactivatedAtUtc,
            DeactivationReason = customer.DeactivationReason,
            ClosedAtUtc = customer.ClosedAtUtc,
            ClosureReason = customer.ClosureReason,
            ChituSlots = customer.ChituSlots.Select(s => new AdminCustomerSlotDto
            {
                Id = s.Id,
                SlotNumber = s.SlotNumber,
                PlanName = s.ChituPlan?.Name ?? "Chitu Savings Plan",
                PlanCode = s.ChituPlan?.Code ?? "CHITU",
                MonthlyInstalmentAmount = s.ChituPlan?.MonthlyInstalmentAmount ?? 0,
                Status = s.Status.ToString(),
                ActivatedAtUtc = s.ActivatedAtUtc
            }).ToList(),
            JewelleryEnrolments = customer.JewelleryEnrolments.Select(e => new AdminCustomerEnrolmentDto
            {
                Id = e.Id,
                PlanName = e.JewelleryPlan?.Name ?? "Jewellery Gold Plan",
                PlanCode = e.JewelleryPlan?.Code ?? "JEW",
                TargetWeightGrams = e.TargetProductWeightGrams,
                TotalSavedGrams = e.TotalSavedGrams,
                TotalContributedCurrency = e.TotalContributedCurrency,
                Status = e.Status.ToString(),
                StartDate = e.StartDate,
                TargetEndDate = e.TargetEndDate
            }).ToList()
        };
    }

    public async Task<CustomerStatisticsDto> GetCustomerStatisticsAsync(
        Guid currentStaffId,
        string? search = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, cancellationToken);

        IQueryable<Customer> query = _db.Customers.AsNoTracking();

        // Branch-level enforcement
        if (isSuperAdmin)
        {
            if (branchId.HasValue && branchId.Value != Guid.Empty)
            {
                query = query.Where(c => c.PrimaryBranchId == branchId.Value);
            }
        }
        else
        {
            if (branchId.HasValue && branchId.Value != Guid.Empty)
            {
                if (!permittedBranchIds.Contains(branchId.Value))
                {
                    throw new UnauthorizedAccessException("You do not have permission to view customer statistics for this branch.");
                }
                query = query.Where(c => c.PrimaryBranchId == branchId.Value);
            }
            else
            {
                query = query.Where(c => permittedBranchIds.Contains(c.PrimaryBranchId));
            }
        }

        // Search filter (Name, Phone, Email, NIC)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(term)) ||
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                (c.Nic != null && c.Nic.ToLower().Contains(term)));
        }

        // Calculate statistics across matching records before pagination.
        // Status filter is intentionally NOT applied here so Active and Inactive counts
        // remain independent and accurate when filtering the table by status.
        var totalCustomers = await query.CountAsync(cancellationToken);
        var activeCustomers = await query.CountAsync(c => c.IsActive, cancellationToken);
        var inactiveCustomers = await query.CountAsync(c => !c.IsActive, cancellationToken);

        return new CustomerStatisticsDto
        {
            TotalCustomers = totalCustomers,
            ActiveCustomers = activeCustomers,
            InactiveCustomers = inactiveCustomers
        };
    }

    public async Task<AdminCustomerDetailDto> CreateCustomerAsync(
        Guid currentStaffId,
        CreateCustomerRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, cancellationToken);

        // 1. Validation and normalization
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2)
        {
            throw new ArgumentException("Full name must be at least 2 characters long.");
        }

        if (!ValidationHelper.TryNormalizePhone(request.PhoneNumber, out var normalizedPhone))
        {
            throw new ArgumentException("Invalid phone number format. Expected format: +947XXXXXXXX or 07XXXXXXXX.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !ValidationHelper.IsValidEmail(request.Email))
        {
            throw new ArgumentException("Please provide a valid email address.");
        }
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (!ValidationHelper.TryNormalizeNic(request.Nic, out var normalizedNic))
        {
            throw new ArgumentException("Invalid Sri Lankan NIC format. Expected 9 digits followed by V/X or 12 digits.");
        }

        // 2. Prevent duplicate NICs and contacts assigned to another customer
        var nicExists = await _db.Customers.AnyAsync(c => c.Nic == normalizedNic, cancellationToken);
        if (nicExists)
        {
            throw new InvalidOperationException("A customer account with this National Identity Card (NIC) already exists.");
        }

        var phoneExists = await _db.Customers.AnyAsync(c => c.PhoneNumber == normalizedPhone, cancellationToken);
        if (phoneExists)
        {
            throw new InvalidOperationException("A customer account with this phone number already exists.");
        }

        var emailExists = await _db.Customers.AnyAsync(c => c.Email == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException("A customer account with this email address already exists.");
        }

        // 3. Branch scope resolution
        Guid targetBranchId;
        if (isSuperAdmin)
        {
            if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
            {
                var branchExists = await _db.Branches.AnyAsync(b => b.Id == request.BranchId.Value, cancellationToken);
                if (!branchExists)
                {
                    throw new ArgumentException("Specified branch not found.");
                }
                targetBranchId = request.BranchId.Value;
            }
            else
            {
                var defaultBranch = await _db.Branches.FirstOrDefaultAsync(b => b.Code == "JAF-01", cancellationToken)
                    ?? await _db.Branches.FirstAsync(cancellationToken);
                targetBranchId = defaultBranch.Id;
            }
        }
        else
        {
            if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
            {
                if (!permittedBranchIds.Contains(request.BranchId.Value))
                {
                    throw new UnauthorizedAccessException("Branch staff can create customers only in their permitted branch.");
                }
                targetBranchId = request.BranchId.Value;
            }
            else
            {
                if (permittedBranchIds.Count == 0)
                {
                    throw new UnauthorizedAccessException("Staff member does not have any assigned branch.");
                }
                targetBranchId = permittedBranchIds.First();
            }
        }

        // 4. Create customer record (Admin-entered contacts remain unverified until verified via OTP)
        var customer = new Customer
        {
            FullName = request.FullName.Trim(),
            PhoneNumber = normalizedPhone,
            Email = normalizedEmail,
            Nic = normalizedNic,
            PrimaryBranchId = targetBranchId,
            IsPhoneVerified = false,
            IsEmailVerified = false,
            IsActive = true,
            IsProfileComplete = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);

        // 5. Audit creation without logging full NIC
        await _auditService.LogActionAsync(
            actorType: "Staff",
            actorId: currentStaffId,
            action: "CUSTOMER_CREATED_BY_ADMIN",
            targetEntity: "Customer",
            targetId: customer.Id.ToString(),
            branchId: targetBranchId,
            before: null,
            after: new
            {
                customer.FullName,
                PhoneNumber = normalizedPhone,
                Email = normalizedEmail,
                MaskedNic = MaskNic(normalizedNic),
                BranchId = targetBranchId
            },
            ipAddress: ipAddress);

        return await GetCustomerByIdAsync(currentStaffId, customer.Id, cancellationToken);
    }

    public async Task<AdminCustomerDetailDto> UpdateCustomerStatusAsync(
        Guid currentStaffId,
        Guid customerId,
        UpdateCustomerStatusRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, cancellationToken);

        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer == null)
        {
            throw new KeyNotFoundException($"Customer with ID {customerId} was not found.");
        }

        // Branch-level enforcement
        if (!isSuperAdmin && !permittedBranchIds.Contains(customer.PrimaryBranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission to manage customers outside your permitted branch scope.");
        }

        if (!request.IsActive)
        {
            // Deactivation requires reason
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new ArgumentException("A reason is required to deactivate a customer account.");
            }

            customer.IsActive = false;
            customer.DeactivatedAtUtc = DateTimeOffset.UtcNow;
            customer.DeactivationReason = request.Reason.Trim();
            customer.DeactivatedByStaffId = currentStaffId;

            // Revoke sessions/refresh tokens immediately
            var activeRefreshTokens = await _db.RefreshTokens
                .Where(r => r.CustomerId == customer.Id && !r.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var token in activeRefreshTokens)
            {
                token.IsRevoked = true;
                token.RevokedAtUtc = DateTimeOffset.UtcNow;
                token.RevokedByIp = ipAddress;
            }

            await _auditService.LogActionAsync(
                actorType: "Staff",
                actorId: currentStaffId,
                action: "CUSTOMER_DEACTIVATED",
                targetEntity: "Customer",
                targetId: customer.Id.ToString(),
                branchId: customer.PrimaryBranchId,
                before: new { IsActive = true },
                after: new { IsActive = false, Reason = customer.DeactivationReason },
                ipAddress: ipAddress);
        }
        else
        {
            // Reactivation
            customer.IsActive = true;
            customer.DeactivatedAtUtc = null;
            customer.DeactivationReason = null;
            customer.DeactivatedByStaffId = null;

            await _auditService.LogActionAsync(
                actorType: "Staff",
                actorId: currentStaffId,
                action: "CUSTOMER_REACTIVATED",
                targetEntity: "Customer",
                targetId: customer.Id.ToString(),
                branchId: customer.PrimaryBranchId,
                before: new { IsActive = false },
                after: new { IsActive = true, Reason = request.Reason },
                ipAddress: ipAddress);
        }

        customer.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetCustomerByIdAsync(currentStaffId, customer.Id, cancellationToken);
    }

    private async Task<(Staff Caller, bool IsSuperAdmin, List<Guid> PermittedBranchIds)> VerifyStaffAccessAsync(
        Guid staffId,
        CancellationToken cancellationToken)
    {
        var staff = await _db.StaffMembers
            .Include(s => s.Roles).ThenInclude(r => r.Role)
            .Include(s => s.AssignedBranches)
            .FirstOrDefaultAsync(s => s.Id == staffId, cancellationToken);

        if (staff == null || !staff.IsActive)
        {
            throw new UnauthorizedAccessException("Unauthorized staff access or account is inactive.");
        }

        var isSuperAdmin = staff.Roles.Any(r =>
            r.Role?.Name == "Super Admin" ||
            r.Role?.RoleType == StaffRoleType.SuperAdmin);

        var permittedBranchIds = staff.AssignedBranches.Select(b => b.BranchId).ToList();

        return (staff, isSuperAdmin, permittedBranchIds);
    }

    public static string? MaskNic(string? nic)
    {
        if (string.IsNullOrWhiteSpace(nic))
        {
            return null;
        }

        var trimmed = nic.Trim();
        if (trimmed.Length <= 5)
        {
            return trimmed;
        }

        // If format is 12-digit (e.g., 199512345678) -> 199512*****
        if (trimmed.Length == 12)
        {
            return trimmed.Substring(0, 6) + "******";
        }

        // If format is 10-char (e.g. 851234567V) -> 85123***V
        if (trimmed.Length == 10 && char.IsLetter(trimmed[^1]))
        {
            return trimmed.Substring(0, 5) + "****" + trimmed[^1];
        }

        // General mask: keep first 5 characters, mask remaining
        var visible = Math.Min(5, trimmed.Length / 2);
        var maskedCount = trimmed.Length - visible;
        return trimmed.Substring(0, visible) + new string('*', maskedCount);
    }
}
