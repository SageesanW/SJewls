using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Customer Authentication");

        // 1. POST /api/v1/auth/otp/request
        group.MapPost("/otp/request", async (
            [FromBody] RequestOtpRequest request,
            IOtpService otpService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Contact))
            {
                return Results.BadRequest(new { message = "Phone number or email address is required." });
            }

            var result = await otpService.RequestOtpAsync(request.Contact);
            if (!result.Success)
            {
                return Results.BadRequest(new { message = result.Message, cooldownSeconds = result.CooldownSeconds });
            }

            return Results.Ok(result);
        })
        .WithName("RequestOtp")
        .WithSummary("Request verification OTP via Phone or Email")
        .WithDescription("Sends a 6-digit OTP to the provided Sri Lankan mobile number (e.g. +94771234567 or 0771234567) or email address. In development, the code is returned in devOtp.")
        .Produces<RequestOtpResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 2. POST /api/v1/auth/otp/verify
        group.MapPost("/otp/verify", async (
            [FromBody] VerifyOtpRequest request,
            HttpContext httpContext,
            ICustomerAuthService authService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Contact) || string.IsNullOrWhiteSpace(request.Code))
            {
                return Results.BadRequest(new { message = "Contact and verification code are required." });
            }

            try
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var result = await authService.ProcessOtpVerificationAsync(request.Contact, request.Code, ip);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("VerifyOtp")
        .WithSummary("Verify submitted OTP code")
        .WithDescription("Verifies the submitted code. If profile is complete, returns nextAction='Dashboard' with JWT and refresh token. If new/incomplete, returns nextAction='CompleteProfile' with a registrationToken.")
        .Produces<VerifyOtpResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 3. POST /api/v1/auth/registration/complete
        group.MapPost("/registration/complete", async (
            [FromBody] CompleteRegistrationRequest request,
            HttpContext httpContext,
            ICustomerAuthService authService) =>
        {
            try
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var result = await authService.CompleteRegistrationAsync(request, ip);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("CompleteRegistration")
        .WithSummary("Complete customer profile registration")
        .WithDescription("Finalizes registration using the registrationToken. Requires FullName, DateOfBirth (18+), Sri Lankan NIC, and the required additional contact. Returns JWT access and refresh tokens.")
        .Produces<VerifyOtpResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 4. POST /api/v1/auth/token/refresh
        group.MapPost("/token/refresh", async (
            [FromBody] RefreshTokenRequest request,
            HttpContext httpContext,
            ICustomerAuthService authService) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Results.BadRequest(new { message = "Refresh token is required." });
            }

            try
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var result = await authService.RefreshTokenAsync(request.RefreshToken, ip);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
        })
        .WithName("RefreshToken")
        .WithSummary("Exchange refresh token for a new access token")
        .WithDescription("Rotates the refresh token and returns a new active access token.")
        .Produces<RefreshTokenResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // 5. POST /api/v1/auth/logout
        group.MapPost("/logout", async (
            [FromBody] LogoutRequest request,
            HttpContext httpContext,
            ICustomerAuthService authService) =>
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                await authService.LogoutAsync(request.RefreshToken, ip);
            }
            return Results.Ok(new { message = "Logged out successfully." });
        })
        .WithName("Logout")
        .WithSummary("Revoke refresh token and log out")
        .Produces(StatusCodes.Status200OK);

        return app;
    }
}
