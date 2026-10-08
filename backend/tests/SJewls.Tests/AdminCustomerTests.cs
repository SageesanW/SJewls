using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SJewls.Application.DTOs;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class AdminCustomerTests
{
    private static (SJewlsDbContext db, AdminCustomerService customerService, Guid superAdminId, Guid branchStaffId, Guid branch1Id, Guid branch2Id) CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new SJewlsDbContext(options);

        // 1. Roles
        var superAdminRole = new Role { Id = Guid.NewGuid(), Name = "Super Admin", RoleType = StaffRoleType.SuperAdmin };
        var staffRole = new Role { Id = Guid.NewGuid(), Name = "Staff", RoleType = StaffRoleType.Staff };
        db.Roles.AddRange(superAdminRole, staffRole);

        // 2. Branches
        var branch1 = new Branch { Id = Guid.NewGuid(), Code = "JAF-01", Name = "Jaffna Main Branch" };
        var branch2 = new Branch { Id = Guid.NewGuid(), Code = "COL-01", Name = "Colombo Branch" };
        db.Branches.AddRange(branch1, branch2);

        // 3. Staff members
        var superAdmin = new Staff
        {
            Id = Guid.NewGuid(),
            FullName = "Super Admin User",
            Email = "superadmin@sjewls.lk",
            IsActive = true
        };
        superAdmin.Roles.Add(new StaffRole { StaffId = superAdmin.Id, RoleId = superAdminRole.Id, Role = superAdminRole });
        db.StaffMembers.Add(superAdmin);

        var branchStaff = new Staff
        {
            Id = Guid.NewGuid(),
            FullName = "Jaffna Staff User",
            Email = "jaffna.staff@sjewls.lk",
            IsActive = true
        };
        branchStaff.Roles.Add(new StaffRole { StaffId = branchStaff.Id, RoleId = staffRole.Id, Role = staffRole });
        branchStaff.AssignedBranches.Add(new StaffBranch { StaffId = branchStaff.Id, BranchId = branch1.Id, Branch = branch1 });
        db.StaffMembers.Add(branchStaff);

        // 4. Sample Customers
        var customer1 = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Vimalanathan Ramanathan",
            PhoneNumber = "+94771112233",
            Email = "vimal@gmail.com",
            Nic = "199512345678",
            DateOfBirth = new DateOnly(1995, 12, 10),
            PrimaryBranchId = branch1.Id,
            PrimaryBranch = branch1,
            IsActive = true,
            IsProfileComplete = true,
            IsPhoneVerified = true,
            IsEmailVerified = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10)
        };
        customer1.ChituSlots.Add(new ChituSlot
        {
            Id = Guid.NewGuid(),
            SlotNumber = 101,
            Status = ChituSlotStatus.Active,
            ActivatedAtUtc = DateTimeOffset.UtcNow.AddDays(-5),
            ChituPlan = new ChituPlan { Name = "Gold Fortune Plan", Code = "GFP-2026", MonthlyInstalmentAmount = 10000 }
        });

        var customer2 = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Priya Sivakumar",
            PhoneNumber = "+94772223344",
            Email = "priya@gmail.com",
            Nic = "851234567V",
            DateOfBirth = new DateOnly(1985, 4, 15),
            PrimaryBranchId = branch2.Id,
            PrimaryBranch = branch2,
            IsActive = true,
            IsProfileComplete = true,
            IsPhoneVerified = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-5)
        };

        var customer3 = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Saranya Thambirajah",
            PhoneNumber = "+94773334455",
            Email = "saranya@gmail.com",
            Nic = "199055566778",
            PrimaryBranchId = branch1.Id,
            PrimaryBranch = branch1,
            IsActive = true,
            IsProfileComplete = false,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };

        db.Customers.AddRange(customer1, customer2, customer3);
        db.SaveChanges();

        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new AdminCustomerService(db, auditService, NullLogger<AdminCustomerService>.Instance);

        return (db, service, superAdmin.Id, branchStaff.Id, branch1.Id, branch2.Id);
    }

    [Fact]
    public async Task SuperAdmin_CanViewCustomersAcrossAllBranches()
    {
        var (_, service, superAdminId, _, _, _) = CreateTestContext();

        var result = await service.GetCustomersAsync(superAdminId, page: 1, pageSize: 10);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task BranchStaff_CanOnlyViewCustomersInPermittedBranch()
    {
        var (_, service, _, branchStaffId, branch1Id, _) = CreateTestContext();

        var result = await service.GetCustomersAsync(branchStaffId, page: 1, pageSize: 10);

        Assert.Equal(2, result.TotalCount); // Only customer 1 and 3 are in Jaffna branch
        Assert.All(result.Items, c => Assert.Equal(branch1Id, c.PrimaryBranchId));
    }

    [Fact]
    public async Task BranchStaff_QueryingOtherBranch_ThrowsUnauthorized()
    {
        var (_, service, _, branchStaffId, _, branch2Id) = CreateTestContext();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetCustomersAsync(branchStaffId, branchId: branch2Id));
    }

    [Fact]
    public async Task Search_FiltersBy_Name_Phone_Email_And_Nic()
    {
        var (_, service, superAdminId, _, _, _) = CreateTestContext();

        var byName = await service.GetCustomersAsync(superAdminId, search: "Vimalanathan");
        Assert.Single(byName.Items);
        Assert.Equal("Vimalanathan Ramanathan", byName.Items[0].FullName);

        var byPhone = await service.GetCustomersAsync(superAdminId, search: "2223344");
        Assert.Single(byPhone.Items);
        Assert.Equal("Priya Sivakumar", byPhone.Items[0].FullName);

        var byEmail = await service.GetCustomersAsync(superAdminId, search: "saranya@gmail.com");
        Assert.Single(byEmail.Items);
        Assert.Equal("Saranya Thambirajah", byEmail.Items[0].FullName);

        var byNic = await service.GetCustomersAsync(superAdminId, search: "851234567V");
        Assert.Single(byNic.Items);
        Assert.Equal("Priya Sivakumar", byNic.Items[0].FullName);
    }

    [Fact]
    public async Task CustomerList_ReturnsCustomersWithFullUnmaskedNic_ForInStoreVerification()
    {
        var (db, service, superAdminId, _, _, _) = CreateTestContext();

        var list = await service.GetCustomersAsync(superAdminId, page: 1, pageSize: 10);

        Assert.NotEmpty(list.Items);
        var priya = list.Items.First(c => c.FullName == "Priya Sivakumar");
        Assert.Equal("851234567V", priya.Nic);

        var vimal = list.Items.First(c => c.FullName == "Vimalanathan Ramanathan");
        Assert.Equal("199512345678", vimal.Nic);
    }

    [Fact]
    public async Task CustomerDetail_ReturnsCustomerWithFullUnmaskedNicAndPlans_ForAdmin()
    {
        var (db, service, superAdminId, _, branch1Id, _) = CreateTestContext();

        var targetCustomer = await db.Customers.FirstAsync(c => c.PrimaryBranchId == branch1Id);

        var detail = await service.GetCustomerByIdAsync(superAdminId, targetCustomer.Id);

        Assert.NotNull(detail);
        Assert.Equal(targetCustomer.FullName, detail.FullName);
        Assert.Equal(targetCustomer.Nic, detail.Nic); // Fully visible for admin
        Assert.Equal("199512345678", detail.Nic);
        Assert.Single(detail.ChituSlots);
        Assert.Equal("Gold Fortune Plan", detail.ChituSlots[0].PlanName);
    }


    [Fact]
    public async Task Metrics_AggregatesAccuratelyForSuperAdminAndBranchStaff()
    {
        var (_, service, superAdminId, branchStaffId, _, _) = CreateTestContext();

        var superAdminMetrics = await service.GetCustomerMetricsAsync(superAdminId);
        Assert.Equal(3, superAdminMetrics.TotalCustomers);
        Assert.Equal(1, superAdminMetrics.TotalSlots);
        Assert.Equal(1, superAdminMetrics.ActiveInvestment);
        Assert.Equal(1, superAdminMetrics.PendingAccounts);

        var staffMetrics = await service.GetCustomerMetricsAsync(branchStaffId);
        Assert.Equal(2, staffMetrics.TotalCustomers);
        Assert.Equal(1, staffMetrics.TotalSlots);
    }

    [Fact]
    public async Task CustomerStatistics_CalculatesTotalActiveAndInactiveSeparately()
    {
        var (db, service, superAdminId, branchStaffId, branch1Id, _) = CreateTestContext();

        // Add an inactive customer in branch1
        db.Customers.Add(new Customer
        {
            FullName = "Inactive Test Customer",
            PhoneNumber = "+94770001111",
            Email = "inactive@test.com",
            Nic = "198011122233",
            PrimaryBranchId = branch1Id,
            IsActive = false,
            DeactivationReason = "Suspended"
        });
        await db.SaveChangesAsync();

        var stats = await service.GetCustomerStatisticsAsync(superAdminId);
        Assert.Equal(4, stats.TotalCustomers);
        Assert.Equal(3, stats.ActiveCustomers);
        Assert.Equal(1, stats.InactiveCustomers);

        // Branch staff should only see statistics for their permitted branch (branch1)
        var branchStats = await service.GetCustomerStatisticsAsync(branchStaffId);
        Assert.Equal(3, branchStats.TotalCustomers);
        Assert.Equal(2, branchStats.ActiveCustomers);
        Assert.Equal(1, branchStats.InactiveCustomers);
    }

    [Fact]
    public async Task CreateCustomer_BySuperAdmin_Succeeds_And_NormalizesData()
    {
        var (db, service, superAdminId, _, branch1Id, _) = CreateTestContext();

        var request = new CreateCustomerRequest
        {
            FullName = "  Meena Kasinathan  ",
            PhoneNumber = "0771234567",
            Email = "  MEENA@GMAIL.COM ",
            Nic = "199212345678",
            BranchId = branch1Id
        };

        var created = await service.CreateCustomerAsync(superAdminId, request, "127.0.0.1");

        Assert.NotNull(created);
        Assert.Equal("Meena Kasinathan", created.FullName);
        Assert.Equal("+94771234567", created.PhoneNumber);
        Assert.Equal("meena@gmail.com", created.Email);
        Assert.Equal("199212345678", created.Nic);
        Assert.False(created.IsPhoneVerified);
        Assert.False(created.IsEmailVerified);
        Assert.True(created.IsActive);
        Assert.False(created.IsProfileComplete);

        var dbCustomer = await db.Customers.FindAsync(created.Id);
        Assert.NotNull(dbCustomer);
        Assert.Equal(branch1Id, dbCustomer.PrimaryBranchId);
    }

    [Fact]
    public async Task CreateCustomer_DuplicateNicOrPhoneOrEmail_ThrowsConflict()
    {
        var (_, service, superAdminId, _, branch1Id, _) = CreateTestContext();

        // 1. Duplicate NIC
        var dupNicReq = new CreateCustomerRequest
        {
            FullName = "Duplicate NIC Test",
            PhoneNumber = "0779998888",
            Email = "unique@gmail.com",
            Nic = "199512345678", // Existing in CreateTestContext
            BranchId = branch1Id
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCustomerAsync(superAdminId, dupNicReq, "127.0.0.1"));

        // 2. Duplicate Phone
        var dupPhoneReq = new CreateCustomerRequest
        {
            FullName = "Duplicate Phone Test",
            PhoneNumber = "0771112233", // Existing in CreateTestContext (+94771112233)
            Email = "unique2@gmail.com",
            Nic = "199912345678",
            BranchId = branch1Id
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCustomerAsync(superAdminId, dupPhoneReq, "127.0.0.1"));

        // 3. Duplicate Email
        var dupEmailReq = new CreateCustomerRequest
        {
            FullName = "Duplicate Email Test",
            PhoneNumber = "0779998888",
            Email = "vimal@gmail.com", // Existing in CreateTestContext
            Nic = "199912345678",
            BranchId = branch1Id
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCustomerAsync(superAdminId, dupEmailReq, "127.0.0.1"));
    }

    [Fact]
    public async Task CreateCustomer_BranchStaff_RestrictedToPermittedBranch()
    {
        var (_, service, _, branchStaffId, _, branch2Id) = CreateTestContext();

        // Branch staff is assigned to branch1, trying to create for branch2
        var req = new CreateCustomerRequest
        {
            FullName = "Branch Restriction Test",
            PhoneNumber = "0775556677",
            Email = "branchtest@gmail.com",
            Nic = "199812345678",
            BranchId = branch2Id
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateCustomerAsync(branchStaffId, req, "127.0.0.1"));
    }

    [Fact]
    public async Task CustomerStatus_DeactivationRequiresReason_AndRevokesTokens()
    {
        var (db, service, superAdminId, _, branch1Id, _) = CreateTestContext();

        var customer = await db.Customers.FirstAsync(c => c.PrimaryBranchId == branch1Id);

        // Add an active refresh token for this customer
        var token = new RefreshToken
        {
            CustomerId = customer.Id,
            Token = "active-test-refresh-token",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false
        };
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();

        // 1. Deactivating without reason fails
        var emptyReasonReq = new UpdateCustomerStatusRequest { IsActive = false, Reason = "" };
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateCustomerStatusAsync(superAdminId, customer.Id, emptyReasonReq, "127.0.0.1"));

        // 2. Deactivating with reason succeeds
        var deactivateReq = new UpdateCustomerStatusRequest { IsActive = false, Reason = "Customer requested account suspension" };
        var updated = await service.UpdateCustomerStatusAsync(superAdminId, customer.Id, deactivateReq, "127.0.0.1");

        Assert.False(updated.IsActive);
        Assert.NotNull(updated.DeactivatedAtUtc);
        Assert.Equal("Customer requested account suspension", updated.DeactivationReason);

        // Verify refresh token is revoked
        var dbToken = await db.RefreshTokens.FirstAsync(t => t.Token == "active-test-refresh-token");
        Assert.True(dbToken.IsRevoked);
        Assert.NotNull(dbToken.RevokedAtUtc);

        // 3. Reactivation restores active status
        var activateReq = new UpdateCustomerStatusRequest { IsActive = true, Reason = "Account reviewed and reactivated" };
        var reactivated = await service.UpdateCustomerStatusAsync(superAdminId, customer.Id, activateReq, "127.0.0.1");

        Assert.True(reactivated.IsActive);
        Assert.Null(reactivated.DeactivatedAtUtc);
        Assert.Null(reactivated.DeactivationReason);
    }
}

