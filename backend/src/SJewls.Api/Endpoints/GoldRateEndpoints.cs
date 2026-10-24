using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class GoldRateEndpoints
{
    public static void MapGoldRateEndpoints(this IEndpointRouteBuilder app)
    {
        // =========================================================================
        // 1. Customer & Public Gold Rate Endpoint
        // =========================================================================
        app.MapGet("/api/v1/customer/gold-rate", async (
            [FromQuery] Guid? branchId,
            [FromServices] IGoldRateService goldRateService,
            CancellationToken ct) =>
        {
            var rate = await goldRateService.GetCustomerRateAsync(branchId, ct);
            return Results.Ok(rate);
        })
        .AllowAnonymous()
        .WithTags("Customer Jewellery Plans")
        .WithName("CustomerGetGoldRate")
        .WithSummary("Get Current Gold Rates")
        .WithDescription("Retrieves the authoritative current gold rates (24K and 22K in LKR) for customer display and quote estimation. Reflects active store manual overrides or live market feeds.")
        .Produces<CustomerGoldRateResponse>(StatusCodes.Status200OK);

        // =========================================================================
        // 2. Admin Gold Rate Management Endpoints
        // =========================================================================
        var adminGroup = app.MapGroup("/api/v1/admin/gold-rates")
            .RequireAuthorization("StaffOnly")
            .WithTags("Admin Gold Rates");

        adminGroup.MapGet("/overview", async (
            [FromQuery] Guid? branchId,
            [FromServices] IGoldRateService goldRateService,
            CancellationToken ct) =>
        {
            var overview = await goldRateService.GetOverviewAsync(branchId, ct);
            return Results.Ok(overview);
        })
        .WithName("AdminGetGoldRateOverview")
        .WithSummary("Get Gold Rate Overview")
        .WithDescription("Retrieves real-time live API spot market rates, current effective store rates, manual override settings, and branch statuses.")
        .Produces<GoldRateOverviewDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        adminGroup.MapPost("/override", async (
            [FromBody] SetGoldRateOverrideRequest request,
            ClaimsPrincipal user,
            [FromServices] IGoldRateService goldRateService,
            CancellationToken ct) =>
        {
            if (request.Rate24k <= 0)
            {
                return Results.BadRequest(new { message = "24K gold rate per gram must be greater than zero." });
            }

            var staffIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;
            Guid.TryParse(staffIdClaim, out var staffId);
            if (staffId == Guid.Empty)
            {
                staffId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
            }

            var updatedOverview = await goldRateService.SetOverrideAsync(request, staffId, ct);
            return Results.Ok(updatedOverview);
        })
        .WithName("AdminSetGoldRateOverride")
        .WithSummary("Set Manual Gold Rate Override")
        .WithDescription("Configures or updates the manual gold rate override (24K and 22K per gram) for a branch or globally. When enabled, this rate immediately overrides live market feeds across customer quotes and purchases.")
        .Produces<GoldRateOverviewDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        adminGroup.MapPost("/override/toggle", async (
            [FromBody] ToggleGoldRateOverrideRequest request,
            ClaimsPrincipal user,
            [FromServices] IGoldRateService goldRateService,
            CancellationToken ct) =>
        {
            var staffIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;
            Guid.TryParse(staffIdClaim, out var staffId);
            if (staffId == Guid.Empty)
            {
                staffId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
            }

            var updatedOverview = await goldRateService.ToggleOverrideAsync(request, staffId, ct);
            return Results.Ok(updatedOverview);
        })
        .WithName("AdminToggleGoldRateOverride")
        .WithSummary("Toggle Manual Override On/Off")
        .WithDescription("Quickly toggles manual rate override on or off to switch between custom store rates and live market feeds.")
        .Produces<GoldRateOverviewDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        adminGroup.MapGet("/history", async (
            [FromQuery] Guid? branchId,
            [FromQuery] int? limit,
            [FromServices] IGoldRateService goldRateService,
            CancellationToken ct) =>
        {
            var history = await goldRateService.GetHistoryAsync(branchId, limit ?? 50, ct);
            return Results.Ok(history);
        })
        .WithName("AdminGetGoldRateHistory")
        .WithSummary("Get Gold Rate Audit History")
        .WithDescription("Retrieves the audited history of all gold rate changes, timestamps, and the staff members who recorded them.")
        .Produces<List<GoldRateHistoryDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
