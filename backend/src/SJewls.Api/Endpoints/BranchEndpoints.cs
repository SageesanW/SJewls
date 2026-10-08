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
    }
}
