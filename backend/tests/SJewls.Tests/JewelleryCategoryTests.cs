using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class JewelleryCategoryTests
{
    private (SJewlsDbContext db, Guid superAdminId, Guid branchStaffId, Guid branch1Id, Guid branch2Id) SetupTestDatabase()
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new SJewlsDbContext(options);

        // Seed Branches
        var branch1 = new Branch { Id = Guid.NewGuid(), Code = "JAF-01", Name = "Jaffna Branch", IsActive = true };
        var branch2 = new Branch { Id = Guid.NewGuid(), Code = "COL-01", Name = "Colombo Branch", IsActive = true };
        db.Branches.AddRange(branch1, branch2);

        // Seed Roles
        var superAdminRole = new Role { Id = Guid.NewGuid(), Name = "Super Admin" };
        var staffRole = new Role { Id = Guid.NewGuid(), Name = "Staff" };
        db.Roles.AddRange(superAdminRole, staffRole);

        // Seed Super Admin
        var superAdmin = new Staff
        {
            Id = Guid.NewGuid(),
            FullName = "Super Administrator",
            Email = "superadmin@sjewls.lk",
            IsActive = true
        };
        db.StaffMembers.Add(superAdmin);
        db.StaffRoles.Add(new StaffRole { Id = Guid.NewGuid(), StaffId = superAdmin.Id, RoleId = superAdminRole.Id, Role = superAdminRole });
        db.StaffBranches.Add(new StaffBranch { Id = Guid.NewGuid(), StaffId = superAdmin.Id, BranchId = branch1.Id });
        db.StaffBranches.Add(new StaffBranch { Id = Guid.NewGuid(), StaffId = superAdmin.Id, BranchId = branch2.Id });

        // Seed Branch Staff (assigned only to branch1)
        var branchStaff = new Staff
        {
            Id = Guid.NewGuid(),
            FullName = "Jaffna Staff",
            Email = "staff@sjewls.lk",
            IsActive = true
        };
        db.StaffMembers.Add(branchStaff);
        db.StaffRoles.Add(new StaffRole { Id = Guid.NewGuid(), StaffId = branchStaff.Id, RoleId = staffRole.Id, Role = staffRole });
        db.StaffBranches.Add(new StaffBranch { Id = Guid.NewGuid(), StaffId = branchStaff.Id, BranchId = branch1.Id });

        db.SaveChanges();

        return (db, superAdmin.Id, branchStaff.Id, branch1.Id, branch2.Id);
    }

    [Fact]
    public async Task CreateCategory_ValidRequest_CreatesSuccessfullyAndComputesNormalizedName()
    {
        var (db, superAdminId, _, branch1Id, _) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryCategoryService(db, auditService, NullLogger<JewelleryCategoryService>.Instance);

        var request = new CreateJewelleryCategoryRequest
        {
            Name = "Bridal Collection",
            Description = "Premium 22K bridal jewellery sets",
            ImageUrl = "https://example.com/bridal.webp",
            BranchId = branch1Id,
            IsActive = true
        };

        var result = await service.CreateCategoryAsync(request, superAdminId);

        Assert.NotNull(result);
        Assert.Equal("Bridal Collection", result.Name);
        Assert.Equal(branch1Id, result.BranchId);
        Assert.True(result.IsActive);
        Assert.Equal(0, result.PlanCount);

        var entity = await db.JewelleryCategories.FirstOrDefaultAsync(c => c.Id == result.Id);
        Assert.NotNull(entity);
        Assert.Equal("BRIDAL COLLECTION", entity.NormalizedName);
    }

    [Fact]
    public async Task CreateCategory_DuplicateNormalizedNameInSameBranch_ThrowsInvalidOperationException()
    {
        var (db, superAdminId, _, branch1Id, _) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryCategoryService(db, auditService, NullLogger<JewelleryCategoryService>.Instance);

        var request1 = new CreateJewelleryCategoryRequest
        {
            Name = "Gold Bars",
            BranchId = branch1Id,
            IsActive = true
        };
        await service.CreateCategoryAsync(request1, superAdminId);

        // Attempt to create duplicate with varying casing and spaces
        var request2 = new CreateJewelleryCategoryRequest
        {
            Name = "  gold bars  ",
            BranchId = branch1Id,
            IsActive = true
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateCategoryAsync(request2, superAdminId));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task CreateCategory_SameNameInDifferentBranches_Allowed()
    {
        var (db, superAdminId, _, branch1Id, branch2Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryCategoryService(db, auditService, NullLogger<JewelleryCategoryService>.Instance);

        var reqBranch1 = new CreateJewelleryCategoryRequest
        {
            Name = "Thali Kodi",
            BranchId = branch1Id,
            IsActive = true
        };
        var reqBranch2 = new CreateJewelleryCategoryRequest
        {
            Name = "Thali Kodi",
            BranchId = branch2Id,
            IsActive = true
        };

        var cat1 = await service.CreateCategoryAsync(reqBranch1, superAdminId);
        var cat2 = await service.CreateCategoryAsync(reqBranch2, superAdminId);

        Assert.NotNull(cat1);
        Assert.NotNull(cat2);
        Assert.NotEqual(cat1.BranchId, cat2.BranchId);
    }

    [Fact]
    public async Task CreateCategory_BranchStaffAttemptingOtherBranch_ThrowsUnauthorizedAccessException()
    {
        var (db, _, branchStaffId, _, branch2Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryCategoryService(db, auditService, NullLogger<JewelleryCategoryService>.Instance);

        // Branch staff is assigned only to branch1, attempting to target branch2
        var request = new CreateJewelleryCategoryRequest
        {
            Name = "Necklace",
            BranchId = branch2Id,
            IsActive = true
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateCategoryAsync(request, branchStaffId));
    }

    [Fact]
    public async Task UpdateCategory_UpdatesFieldsAndProtectsUniqueness()
    {
        var (db, superAdminId, _, branch1Id, _) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryCategoryService(db, auditService, NullLogger<JewelleryCategoryService>.Instance);

        var cat1 = await service.CreateCategoryAsync(new CreateJewelleryCategoryRequest
        {
            Name = "Category A",
            BranchId = branch1Id,
            IsActive = true
        }, superAdminId);

        var cat2 = await service.CreateCategoryAsync(new CreateJewelleryCategoryRequest
        {
            Name = "Category B",
            BranchId = branch1Id,
            IsActive = true
        }, superAdminId);

        // Updating cat1 with new name & description
        var updateReq = new UpdateJewelleryCategoryRequest
        {
            Name = "Category A Updated",
            Description = "Updated Description",
            IsActive = true
        };
        var updated = await service.UpdateCategoryAsync(cat1.Id, updateReq, superAdminId);
        Assert.Equal("Category A Updated", updated.Name);
        Assert.Equal("Updated Description", updated.Description);

        // Attempting to rename cat1 to Category B (conflict)
        var conflictReq = new UpdateJewelleryCategoryRequest
        {
            Name = "Category B",
            IsActive = true
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateCategoryAsync(cat1.Id, conflictReq, superAdminId));
    }

    [Fact]
    public async Task UpdateCategoryStatus_TogglesActiveStateWithoutAffectingPlans()
    {
        var (db, superAdminId, _, branch1Id, _) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryCategoryService(db, auditService, NullLogger<JewelleryCategoryService>.Instance);

        var cat = await service.CreateCategoryAsync(new CreateJewelleryCategoryRequest
        {
            Name = "Daily Wear",
            BranchId = branch1Id,
            IsActive = true
        }, superAdminId);

        Assert.True(cat.IsActive);

        var deactivated = await service.UpdateCategoryStatusAsync(cat.Id, false, superAdminId);
        Assert.False(deactivated.IsActive);

        var reactivated = await service.UpdateCategoryStatusAsync(cat.Id, true, superAdminId);
        Assert.True(reactivated.IsActive);
    }

    [Fact]
    public async Task DeleteCategory_WithAssociatedPlans_ThrowsInvalidOperationException()
    {
        var (db, superAdminId, _, branch1Id, _) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryCategoryService(db, auditService, NullLogger<JewelleryCategoryService>.Instance);

        var cat = await service.CreateCategoryAsync(new CreateJewelleryCategoryRequest
        {
            Name = "Antique Jewellery",
            BranchId = branch1Id,
            IsActive = true
        }, superAdminId);

        // Associate a plan to this category
        var plan = new JewelleryPlan
        {
            Id = Guid.NewGuid(),
            Code = "JP-ANT-01",
            Name = "Royal Antique Plan",
            BranchId = branch1Id,
            CategoryId = cat.Id,
            IsActive = true
        };
        db.JewelleryPlans.Add(plan);
        await db.SaveChangesAsync();

        // Attempt deletion
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteCategoryAsync(cat.Id, superAdminId));
        Assert.Contains("associated jewellery plan(s)", ex.Message);
        Assert.Contains("deactivate", ex.Message);
    }

    [Fact]
    public async Task DeleteCategory_UnusedCategory_DeletesSuccessfully()
    {
        var (db, superAdminId, _, branch1Id, _) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryCategoryService(db, auditService, NullLogger<JewelleryCategoryService>.Instance);

        var cat = await service.CreateCategoryAsync(new CreateJewelleryCategoryRequest
        {
            Name = "Temporary Category",
            BranchId = branch1Id,
            IsActive = false
        }, superAdminId);

        await service.DeleteCategoryAsync(cat.Id, superAdminId);

        var fetched = await service.GetCategoryByIdAsync(cat.Id, superAdminId);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task GetActiveCategoriesForCustomerBrowsing_ReturnsOnlyActiveCategories()
    {
        var (db, superAdminId, _, branch1Id, _) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryCategoryService(db, auditService, NullLogger<JewelleryCategoryService>.Instance);

        await service.CreateCategoryAsync(new CreateJewelleryCategoryRequest
        {
            Name = "Active Category 1",
            BranchId = branch1Id,
            IsActive = true
        }, superAdminId);

        await service.CreateCategoryAsync(new CreateJewelleryCategoryRequest
        {
            Name = "Inactive Category 2",
            BranchId = branch1Id,
            IsActive = false
        }, superAdminId);

        var browsingList = await service.GetActiveCategoriesForCustomerBrowsingAsync(branch1Id);
        Assert.Single(browsingList);
        Assert.Equal("Active Category 1", browsingList[0].Name);
    }

    [Fact]
    public void ValidateImageFile_ValidatesFileFormatsAndSize()
    {
        var inMemoryConfig = new ConfigurationBuilder().Build();
        var storage = new SupabaseStorageService(new HttpClient(), inMemoryConfig, NullLogger<SupabaseStorageService>.Instance);

        // Valid 1MB PNG
        Assert.True(storage.ValidateImageFile("photo.png", "image/png", 1024 * 1024, out var error1));
        Assert.Null(error1);

        // Valid 500KB WEBP
        Assert.True(storage.ValidateImageFile("banner.webp", "image/webp", 500 * 1024, out var error2));
        Assert.Null(error2);

        // Invalid: Exceeds 2MB (e.g. 3MB)
        Assert.False(storage.ValidateImageFile("large.png", "image/png", 3 * 1024 * 1024, out var error3));
        Assert.Contains("exceeds the maximum allowed limit of 2MB", error3);

        // Invalid: Unsupported extension (.pdf)
        Assert.False(storage.ValidateImageFile("document.pdf", "application/pdf", 100 * 1024, out var error4));
        Assert.Contains("Unsupported file format", error4);

        // Invalid: Empty file
        Assert.False(storage.ValidateImageFile("empty.jpg", "image/jpeg", 0, out var error5));
        Assert.Contains("empty", error5);
    }
}
