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

        // 0. POST /api/v1/auth/check
        group.MapPost("/check", async (
            [FromBody] CheckContactRequest request,
            ICustomerAuthService authService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Contact))
            {
                return Results.BadRequest(new { message = "Phone number or email address is required." });
            }

            try
            {
                var result = await authService.CheckContactAsync(request.Contact);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("CheckContact")
        .WithSummary("Check if contact exists in database and determine next step (Login or Register)")
        .WithDescription("Verifies the phone number or email address against existing customers in the database. Returns whether the customer exists, profile completion state, and recommended next action ('Login' or 'Register').")
        .Produces<CheckContactResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

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
        .WithSummary("Request verification OTP via Phone (Text.lk SMS) or Email (Gmail SMTP)")
        .WithDescription(
            "Generates and dispatches a 6-digit verification code to the specified contact.\n\n" +
            "**Delivery Modes:**\n" +
            "• **Email Address**: Dispatched via real Gmail SMTP (`smtp.gmail.com:587`, STARTTLS) from `w.sageesan@gmail.com` with subject 'Your SJewls verification code'. Returns 200 OK only after the SMTP server explicitly accepts the message. If delivery fails (e.g., SMTP auth/network failure), the challenge is immediately invalidated and a safe 400 error is returned (no fallback to mock).\n" +
            "• **Phone Number**: Dispatched via real Text.lk SMS Gateway (`https://app.text.lk/api/v3/sms/send`, Bearer auth) with sender ID `TextLKDemo`. Returns 200 OK only after the SMS gateway accepts delivery. On failure, the challenge is immediately invalidated and a safe 400 error is returned.\n\n" +
            "**Outcomes & Error Responses:**\n" +
            "• `200 OK`: Verification code successfully accepted by delivery provider. Valid for 5 minutes (resend cooldown 60 seconds).\n" +
            "• `400 Bad Request`: Active resend cooldown in effect, invalid phone/email format, or provider delivery failure.")
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
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("VerifyOtp")
        .WithSummary("Verify submitted OTP code and authenticate or continue registration")
        .WithDescription(
            "Verifies the submitted 6-digit OTP against active challenges.\n\n" +
            "**Outcomes & Responses:**\n" +
            "• `200 OK (nextAction: 'Dashboard')`: Customer exists and profile is complete. Returns JWT access token, refresh token, and customer details.\n" +
            "• `200 OK (nextAction: 'CompleteProfile')`: Customer is new or profile is incomplete. Returns temporary 1-hour `registrationToken`.\n" +
            "• `400 Bad Request`: Verification code incorrect (attempts decremented, max 5 allowed), challenge expired (after 5 minutes), or challenge already consumed.\n" +
            "• `401 Unauthorized`: Customer account is deactivated or closed.")
        .Produces<VerifyOtpResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

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
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("CompleteRegistration")
        .WithSummary("Complete customer profile registration")
        .WithDescription("Finalizes customer registration using the registrationToken. Automatically preserves the initially entered OTP-verified contact and saves any provided secondary email/phone number. Requires FullName, DateOfBirth (18+), and valid Sri Lankan NIC. Returns JWT access and refresh tokens.")
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
