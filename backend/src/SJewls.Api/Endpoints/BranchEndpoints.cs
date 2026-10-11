using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class BranchEndpoints
{
    public static void MapBranchEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Public / Mobile Branches Endpoint
        app.MapGet("/api/v1/branches", async (
            IBranchService branchService,
            CancellationToken ct) =>
        {
            var branches = await branchService.GetActiveBranchesAsync(ct);
            return Results.Ok(branches);
        })
        .WithName("GetBranches")
        .WithSummary("List Active Branches")
        .WithDescription("Retrieves all currently active branches for customer registration, store location selection, and mobile lookups.")
        .WithTags("Branches")
        .Produces<List<BranchDto>>(StatusCodes.Status200OK);

        // 2. Admin Branch Management Endpoints
        var adminGroup = app.MapGroup("/api/v1/admin/branches")
            .WithTags("Admin Branches");

        // GET /api/v1/admin/branches
        adminGroup.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            IBranchService branchService,
            CancellationToken ct) =>
        {
            var branches = await branchService.GetAllBranchesAsync(search, isActive, ct);
            return Results.Ok(branches);
        })
        .RequireAuthorization("StaffOnly")
        .WithName("AdminGetBranches")
        .WithSummary("List All Branches")
        .WithDescription("Retrieves all store branches with assigned staff counts and customer counts. Supports keyword search and active/inactive status filtering.")
        .Produces<List<BranchDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // GET /api/v1/admin/branches/{id}
        adminGroup.MapGet("/{id:guid}", async (
            Guid id,
            IBranchService branchService,
            CancellationToken ct) =>
        {
            var branch = await branchService.GetBranchByIdAsync(id, ct);
            if (branch == null)
            {
                return Results.NotFound(new { message = $"Branch with ID '{id}' was not found." });
            }
            return Results.Ok(branch);
        })
        .RequireAuthorization("StaffOnly")
        .WithName("AdminGetBranchById")
        .WithSummary("Get Branch Details")
        .WithDescription("Retrieves detailed branch information including staff assignments and customer statistics.")
        .Produces<BranchDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/v1/admin/branches
        adminGroup.MapPost("/", async (
            [FromBody] CreateBranchRequest request,
            IBranchService branchService,
            CancellationToken ct) =>
        {
            try
            {
                var created = await branchService.CreateBranchAsync(request, ct);
                return Results.Created($"/api/v1/admin/branches/{created.Id}", created);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        })
        .RequireAuthorization("SuperAdminOnly")
        .WithName("AdminCreateBranch")
        .WithSummary("Create New Branch")
        .WithDescription("Creates a new branch. Branch Code must be unique. Requires Super Admin permissions.")
        .Produces<BranchDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/v1/admin/branches/{id}
        adminGroup.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateBranchRequest request,
            IBranchService branchService,
            CancellationToken ct) =>
        {
            try
            {
                var updated = await branchService.UpdateBranchAsync(id, request, ct);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .RequireAuthorization("SuperAdminOnly")
        .WithName("AdminUpdateBranch")
        .WithSummary("Update Branch")
        .WithDescription("Updates store branch details (Name, Address, City, Country, Currency, Timezone, Active status). Requires Super Admin permissions.")
        .Produces<BranchDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // PATCH /api/v1/admin/branches/{id}/status
        adminGroup.MapPatch("/{id:guid}/status", async (
            Guid id,
            [FromBody] UpdateBranchStatusRequest request,
            IBranchService branchService,
            CancellationToken ct) =>
        {
            try
            {
                var updated = await branchService.UpdateBranchStatusAsync(id, request.IsActive, ct);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .RequireAuthorization("SuperAdminOnly")
        .WithName("AdminUpdateBranchStatus")
        .WithSummary("Activate or Deactivate Branch")
        .WithDescription("Toggles branch active status. Deactivating the only remaining active branch is blocked. Requires Super Admin permissions.")
        .Produces<BranchDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // DELETE /api/v1/admin/branches/{id}
        adminGroup.MapDelete("/{id:guid}", async (
            Guid id,
            IBranchService branchService,
            CancellationToken ct) =>
        {
            try
            {
                await branchService.DeleteBranchAsync(id, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        })
        .RequireAuthorization("SuperAdminOnly")
        .WithName("AdminDeleteBranch")
        .WithSummary("Delete Branch")
        .WithDescription("Deletes a branch that has no assigned customers, staff members, or active plans. Requires Super Admin permissions.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 3. Admin Gold Rate Management Endpoints
        app.MapPost("/api/v1/admin/gold-rates", async (
            [FromBody] RecordGoldRateRequest request,
            ClaimsPrincipal user,
            SJewls.Infrastructure.Data.SJewlsDbContext db,
            CancellationToken ct) =>
        {
            if (request.RatePerGram <= 0)
            {
                return Results.BadRequest(new { message = "Rate per gram must be greater than zero." });
            }

            var staffIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;
            Guid.TryParse(staffIdClaim, out var staffId);

            var goldRate = new SJewls.Domain.Entities.GoldRate
            {
                BranchId = request.BranchId,
                Karat = request.Karat <= 0 ? 24 : request.Karat,
                RatePerGram = request.RatePerGram,
                EffectiveFromUtc = request.EffectiveFromUtc ?? DateTimeOffset.UtcNow,
                RecordedByStaffId = staffId != Guid.Empty ? staffId : Guid.Parse("c0000000-0000-0000-0000-000000000001")
            };

            db.GoldRates.Add(goldRate);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/admin/gold-rates/{goldRate.Id}", new
            {
                id = goldRate.Id,
                branchId = goldRate.BranchId,
                karat = goldRate.Karat,
                ratePerGram = goldRate.RatePerGram,
                effectiveFromUtc = goldRate.EffectiveFromUtc
            });
        })
        .RequireAuthorization("StaffOnly")
        .WithTags("Admin Gold Rates")
        .WithName("AdminRecordGoldRate")
        .WithSummary("Record Branch Gold Rate")
        .WithDescription("Records a branch gold rate in LKR per gram for calculations and customer quotes.")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/v1/admin/gold-rates", async (
            [FromQuery] Guid? branchId,
            SJewls.Infrastructure.Data.SJewlsDbContext db,
            CancellationToken ct) =>
        {
            var query = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(db.GoldRates);
            if (branchId.HasValue)
            {
                query = query.Where(r => r.BranchId == branchId.Value);
            }
            var list = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                query.OrderByDescending(r => r.EffectiveFromUtc)
                    .Take(50)
                    .Select(r => new
                    {
                        id = r.Id,
                        branchId = r.BranchId,
                        karat = r.Karat,
                        ratePerGram = r.RatePerGram,
                        effectiveFromUtc = r.EffectiveFromUtc
                    }), ct);
            return Results.Ok(list);
        })
        .RequireAuthorization("StaffOnly")
        .WithTags("Admin Gold Rates")
        .WithName("AdminGetGoldRates")
        .WithSummary("Get Recorded Gold Rates")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}

public record RecordGoldRateRequest(Guid BranchId, int Karat, decimal RatePerGram, DateTimeOffset? EffectiveFromUtc, string? Notes);

