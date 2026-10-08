using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class AdminAuthEndpoints
{
    public static void MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/auth")
            .WithTags("Admin Auth");

        // POST /api/v1/admin/auth/login
        group.MapPost("/login", async (
            [FromBody] StaffLoginRequest request,
            IStaffAuthService authService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            try
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var result = await authService.LoginAsync(request, ip, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
        })
        .WithName("AdminLogin")
        .WithSummary("Staff and Super Admin Login")
        .WithDescription("Authenticates a staff member or Super Admin using their username/email and password. Returns a JWT bearer token and user profile.")
        .Produces<StaffLoginResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireRateLimiting("AuthLimiter");

        // POST /api/v1/admin/auth/logout
        group.MapPost("/logout", async (
            ClaimsPrincipal user,
            IStaffAuthService authService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var staffId))
            {
                return Results.Unauthorized();
            }

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            await authService.LogoutAsync(staffId, ip, ct);
            return Results.Ok(new StaffLogoutResponse());
        })
        .RequireAuthorization("StaffOnly")
        .WithName("AdminLogout")
        .WithSummary("Staff Logout")
        .WithDescription("Invalidates current staff sessions by updating security stamp and logs the logout event.")
        .Produces<StaffLogoutResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // GET /api/v1/admin/auth/me
        group.MapGet("/me", async (
            ClaimsPrincipal user,
            IStaffAuthService authService,
            CancellationToken ct) =>
        {
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var staffId))
            {
                return Results.Unauthorized();
            }

            var stamp = user.FindFirst("security_stamp")?.Value;
            try
            {
                var staffUser = await authService.GetCurrentStaffAsync(staffId, stamp, ct);
                return Results.Ok(staffUser);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
        })
        .RequireAuthorization("StaffOnly")
        .WithName("AdminGetMe")
        .WithSummary("Get Current Staff Profile")
        .WithDescription("Retrieves the authenticated staff member's profile, roles, and branch assignments. Enforces active status and security stamp validity.")
        .Produces<StaffUserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/v1/admin/auth/forgot-password
        group.MapPost("/forgot-password", async (
            [FromBody] ForgotPasswordRequest request,
            IStaffAuthService authService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var origin = httpContext.Request.Headers.Origin.FirstOrDefault() 
                ?? $"{httpContext.Request.Scheme}://{httpContext.Request.Host.Value}";
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            var response = await authService.ForgotPasswordAsync(request, origin, ip, ct);
            return Results.Ok(response);
        })
        .WithName("AdminForgotPassword")
        .WithSummary("Staff Forgot Password")
        .WithDescription("Sends an expiring single-use password reset link to the staff member's email if an active account exists. Returns the same message regardless of whether the email exists.")
        .Produces<ForgotPasswordResponse>(StatusCodes.Status200OK)
        .RequireRateLimiting("AuthLimiter");

        // POST /api/v1/admin/auth/reset-password
        group.MapPost("/reset-password", async (
            [FromBody] ResetPasswordRequest request,
            IStaffAuthService authService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            try
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var response = await authService.ResetPasswordAsync(request, ip, ct);
                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("AdminResetPassword")
        .WithSummary("Staff Reset Password")
        .WithDescription("Validates the single-use reset token and password policy, updates the password hash, invalidates the token and previous sessions, and logs the change.")
        .Produces<ResetPasswordResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .RequireRateLimiting("AuthLimiter");
    }
}
