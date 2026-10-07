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
        .WithSummary("Get authenticated customer profile and contacts")
        .WithDescription("Retrieves customer full name, date of birth, NIC, primary branch, and all primary/secondary contacts.")
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
        .WithSummary("Request OTP to verify secondary profile contact")
        .WithDescription("Sends a 6-digit OTP to the customer's unverified additional contact (email or phone).")
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

        return app;
    }
}
