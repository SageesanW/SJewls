using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class AdminUserEndpoints
{
    public static void MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/users")
            .WithTags("Admin Users");

        // GET /api/v1/admin/users
        group.MapGet("/", async (
            ClaimsPrincipal user,
            IStaffAuthService authService,
            CancellationToken ct) =>
        {
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var currentStaffId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var users = await authService.GetStaffUsersAsync(currentStaffId, ct);
                return Results.Ok(users);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireAuthorization("BranchAdminOrSuperAdmin")
        .WithName("AdminGetUsers")
        .WithSummary("List Staff Users")
        .WithDescription("Retrieves a list of staff accounts. Super Admins view all staff; Branch Admins view staff within their assigned branch. Password hashes are never returned.")
        .Produces<List<StaffUserDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/users
        group.MapPost("/", async (
            [FromBody] CreateStaffUserRequest request,
            ClaimsPrincipal user,
            IStaffAuthService authService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var currentStaffId))
            {
                return Results.Unauthorized();
            }

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var createdUser = await authService.CreateStaffUserAsync(request, currentStaffId, ip, ct);
                return Results.Created($"/api/v1/admin/users/{createdUser.Id}", createdUser);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireAuthorization("SuperAdminOnly")
        .WithName("AdminCreateUser")
        .WithSummary("Create Staff User")
        .WithDescription("Allows Super Admin to provision a new staff or admin user, assigning their role and branch. Validates email, phone, and password policy, and prevents duplicates.")
        .Produces<StaffUserDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status403Forbidden);

        // PATCH /api/v1/admin/users/{id:guid}/status
        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            [FromBody] UpdateStaffStatusRequest request,
            ClaimsPrincipal user,
            IStaffAuthService authService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var currentStaffId))
            {
                return Results.Unauthorized();
            }

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var updatedUser = await authService.UpdateStaffStatusAsync(currentStaffId, id, request, ip, ct);
                return Results.Ok(updatedUser);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { message = "Staff member not found." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireAuthorization("BranchAdminOrSuperAdmin")
        .WithName("AdminUpdateUserStatus")
        .WithSummary("Activate or Deactivate Staff User")
        .WithDescription("Allows authorized administrators to activate or deactivate a staff member. Super Admin can manage all staff and admins (preventing deactivation of the last Super Admin). Staff cannot deactivate themselves. Sessions are immediately invalidated upon deactivation.")
        .Produces<StaffUserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
