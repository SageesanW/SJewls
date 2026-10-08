using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customers")
            .WithTags("Customer Profile & Contacts")
            .RequireAuthorization();

        // 1. GET /api/v1/customers/me
        group.MapGet("/me", async (
            ClaimsPrincipal user,
            ICustomerAuthService authService) =>
        {
            var customerIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? user.FindFirstValue("sub");

            if (!Guid.TryParse(customerIdStr, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var profile = await authService.GetCustomerProfileAsync(customerId);
                return Results.Ok(profile);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { message = "Customer profile not found." });
            }
        })
        .WithName("GetCurrentCustomerProfile")
        .WithSummary("Get authenticated customer profile")
        .WithDescription("Retrieves customer full name, date of birth, NIC, phone number, email address, verification statuses, and primary branch.")
        .Produces<CustomerProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 2. POST /api/v1/customers/me/contacts/otp/request
        group.MapPost("/me/contacts/otp/request", async (
            ClaimsPrincipal user,
            [FromBody] RequestSecondaryContactOtpRequest request,
            ICustomerAuthService authService) =>
        {
            var customerIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? user.FindFirstValue("sub");

            if (!Guid.TryParse(customerIdStr, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var result = await authService.RequestSecondaryContactOtpAsync(customerId, request.Contact);
                if (!result.Success)
                {
                    return Results.BadRequest(new { message = result.Message, cooldownSeconds = result.CooldownSeconds });
                }
                return Results.Ok(result);
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
        .WithName("RequestSecondaryContactOtp")
        .WithSummary("Request OTP to verify secondary profile contact (Email or Phone)")
        .WithDescription(
            "Sends a 6-digit OTP to the customer's unverified secondary contact (email or phone).\n\n" +
            "**Delivery Modes:**\n" +
            "• **Email Address**: Dispatched via real Gmail SMTP (`smtp.gmail.com:587`, STARTTLS) from `w.sageesan@gmail.com`. Invalidates challenge and returns safe 400 error on delivery failure.\n" +
            "• **Phone Number**: Dispatched via real Text.lk SMS Gateway (`https://app.text.lk/api/v3/sms/send`, Bearer auth) with sender ID `TextLKDemo`. Invalidates challenge and returns safe 400 error on delivery failure.\n\n" +
            "**Outcomes & Error Responses:**\n" +
            "• `200 OK`: Verification code successfully dispatched.\n" +
            "• `400 Bad Request`: Delivery failure or invalid contact.\n" +
            "• `404 Not Found`: Customer or pending contact not found.\n" +
            "• `409 Conflict`: Contact already verified.")
        .Produces<RequestOtpResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // 3. POST /api/v1/customers/me/contacts/otp/verify
        group.MapPost("/me/contacts/otp/verify", async (
            ClaimsPrincipal user,
            [FromBody] VerifySecondaryContactOtpRequest request,
            ICustomerAuthService authService) =>
        {
            var customerIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? user.FindFirstValue("sub");

            if (!Guid.TryParse(customerIdStr, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var verified = await authService.VerifySecondaryContactOtpAsync(customerId, request.Contact, request.Code);
                return Results.Ok(new { success = verified, message = "Additional contact verified successfully. You can now use this contact to sign in." });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        })
        .WithName("VerifySecondaryContactOtp")
        .WithSummary("Verify and activate secondary contact for login")
        .WithDescription("Submits the OTP for the additional contact. Once verified, this contact is activated and can also be used for customer login.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 4. POST /api/v1/customers/me/account-closure/otp/request
        group.MapPost("/me/account-closure/otp/request", async (
            ClaimsPrincipal user,
            ICustomerAuthService authService,
            CancellationToken ct) =>
        {
            var customerIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? user.FindFirstValue("sub");

            if (!Guid.TryParse(customerIdStr, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var result = await authService.RequestAccountClosureOtpAsync(customerId, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("RequestAccountClosureOtp")
        .WithSummary("Request OTP for customer self-service account closure")
        .WithDescription("Initiates customer self-service account closure by dispatching a fresh OTP to the customer's verified contact. The challenge is cryptographically bound to the AccountClosure purpose and cannot be substituted with a login OTP.")
        .Produces<RequestClosureOtpResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 5. POST /api/v1/customers/me/account-closure/confirm
        group.MapPost("/me/account-closure/confirm", async (
            ClaimsPrincipal user,
            [FromBody] ConfirmAccountClosureRequest request,
            ICustomerAuthService authService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var customerIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? user.FindFirstValue("sub");

            if (!Guid.TryParse(customerIdStr, out var customerId))
            {
                return Results.Unauthorized();
            }

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var result = await authService.ConfirmAccountClosureAsync(customerId, request, ip, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("ConfirmAccountClosure")
        .WithSummary("Confirm self-service account closure using OTP")
        .WithDescription("Confirms soft closure of the customer account with fresh OTP verification. Marks status as Inactive, records CustomerRequestedClosure timestamp, revokes all active sessions/refresh tokens, and retains all financial, plan, and audit histories for regulatory compliance.")
        .Produces<AccountClosureResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
