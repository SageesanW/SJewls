using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SJewls.Application.DTOs;
using SJewls.Domain.Entities;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class BranchServiceTests
{
    private SJewlsDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new SJewlsDbContext(options);
    }

    [Fact]
    public async Task CreateBranch_ValidRequest_CreatesSuccessfullyAndUpperCasesCode()
    {
        using var db = CreateInMemoryDbContext();
        var service = new BranchService(db, NullLogger<BranchService>.Instance);

        var request = new CreateBranchRequest
        {
            Code = "col-01",
            Name = "Colombo Flagship",
            Address = "45 Galle Road",
            City = "Colombo",
            Country = "Sri Lanka",
            Currency = "LKR",
            Timezone = "Asia/Colombo",
            IsActive = true
        };

        var result = await service.CreateBranchAsync(request);

        Assert.NotNull(result);
        Assert.Equal("COL-01", result.Code);
        Assert.Equal("Colombo Flagship", result.Name);
        Assert.True(result.IsActive);

        var saved = await db.Branches.FirstOrDefaultAsync(b => b.Code == "COL-01");
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task CreateBranch_DuplicateCode_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext();
        db.Branches.Add(new Branch
        {
            Id = Guid.NewGuid(),
            Code = "JAF-01",
            Name = "Jaffna Branch",
            Address = "124 Hospital Rd",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new BranchService(db, NullLogger<BranchService>.Instance);

        var request = new CreateBranchRequest
        {
            Code = "jaf-01",
            Name = "Another Jaffna",
            Address = "Another Rd"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateBranchAsync(request));
    }

    [Fact]
    public async Task GetAllBranches_WithSearchAndFilter_ReturnsMatchingBranchesWithCounts()
    {
        using var db = CreateInMemoryDbContext();
        var branch1 = new Branch { Id = Guid.NewGuid(), Code = "JAF-01", Name = "Jaffna Main", Address = "Hospital Rd", City = "Jaffna", IsActive = true };
        var branch2 = new Branch { Id = Guid.NewGuid(), Code = "COL-01", Name = "Colombo Branch", Address = "Galle Rd", City = "Colombo", IsActive = true };
        var branch3 = new Branch { Id = Guid.NewGuid(), Code = "KAN-01", Name = "Kandy Center", Address = "Dalada Veediya", City = "Kandy", IsActive = false };

        db.Branches.AddRange(branch1, branch2, branch3);

        // Add 1 customer to branch1
        db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Customer 1",
            PrimaryBranchId = branch1.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        // Add 1 staff to branch1
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            FullName = "Staff 1",
            Email = "staff@test.com",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.StaffMembers.Add(staff);
        db.StaffBranches.Add(new StaffBranch { Id = Guid.NewGuid(), StaffId = staff.Id, BranchId = branch1.Id });

        await db.SaveChangesAsync();

        var service = new BranchService(db, NullLogger<BranchService>.Instance);

        // Filter active only
        var activeBranches = await service.GetAllBranchesAsync(null, true);
        Assert.Equal(2, activeBranches.Count);

        // Filter by keyword "Jaffna"
        var searched = await service.GetAllBranchesAsync("Jaffna", null);
        Assert.Single(searched);
        Assert.Equal("JAF-01", searched[0].Code);
        Assert.Equal(1, searched[0].CustomerCount);
        Assert.Equal(1, searched[0].AssignedStaffCount);
    }

    [Fact]
    public async Task UpdateBranch_UpdatesPropertiesProperly()
    {
        using var db = CreateInMemoryDbContext();
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Code = "KAN-01",
            Name = "Old Kandy",
            Address = "Old Address",
            City = "Kandy",
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var service = new BranchService(db, NullLogger<BranchService>.Instance);

        var updateReq = new UpdateBranchRequest
        {
            Name = "Kandy Premium Boutique",
            Address = "New Dalada Maligawa Road",
            City = "Kandy",
            Country = "Sri Lanka",
            Currency = "LKR",
            Timezone = "Asia/Colombo",
            IsActive = true
        };

        var updated = await service.UpdateBranchAsync(branch.Id, updateReq);

        Assert.Equal("Kandy Premium Boutique", updated.Name);
        Assert.Equal("New Dalada Maligawa Road", updated.Address);
    }

    [Fact]
    public async Task UpdateBranchStatus_PreventDeactivatingLastActiveBranch()
    {
        using var db = CreateInMemoryDbContext();
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Code = "JAF-01",
            Name = "Only Active Branch",
            Address = "Hospital Rd",
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var service = new BranchService(db, NullLogger<BranchService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateBranchStatusAsync(branch.Id, false));
    }

    [Fact]
    public async Task DeleteBranch_WithAssignedCustomers_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext();
        var branch1 = new Branch { Id = Guid.NewGuid(), Code = "JAF-01", Name = "Jaffna", Address = "Hospital Rd", IsActive = true };
        var branch2 = new Branch { Id = Guid.NewGuid(), Code = "COL-01", Name = "Colombo", Address = "Galle Rd", IsActive = true };
        db.Branches.AddRange(branch1, branch2);

        db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Assigned Customer",
            PrimaryBranchId = branch1.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new BranchService(db, NullLogger<BranchService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteBranchAsync(branch1.Id));
        Assert.Contains("customer(s) are assigned", ex.Message);
    }

    [Fact]
    public async Task DeleteBranch_UnusedBranch_DeletesSuccessfully()
    {
        using var db = CreateInMemoryDbContext();
        var branch1 = new Branch { Id = Guid.NewGuid(), Code = "JAF-01", Name = "Jaffna", Address = "Hospital Rd", IsActive = true };
        var branch2 = new Branch { Id = Guid.NewGuid(), Code = "UNUSED-01", Name = "Unused Test Branch", Address = "Test Rd", IsActive = false };
        db.Branches.AddRange(branch1, branch2);
        await db.SaveChangesAsync();

        var service = new BranchService(db, NullLogger<BranchService>.Instance);

        await service.DeleteBranchAsync(branch2.Id);

        var found = await db.Branches.FindAsync(branch2.Id);
        Assert.Null(found);
    }
}
