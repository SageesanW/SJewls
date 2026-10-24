using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class CustomerPaymentEndpoints
{
    public static IEndpointRouteBuilder MapCustomerPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customer")
            .RequireAuthorization()
            .WithTags("Customer Unified Payments");

        // 1. GET /api/v1/customer/payments
        group.MapGet("/payments", async (
            ClaimsPrincipal user,
            ICustomerPaymentService service,
            [FromQuery] string? planType,
            [FromQuery] Guid? planId,
            [FromQuery] string? status,
            [FromQuery] DateTimeOffset? startDate,
            [FromQuery] DateTimeOffset? endDate,
            [FromQuery] string? search,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            if (page.HasValue && page.Value <= 0)
            {
                return Results.BadRequest(new { message = "Page number must be greater than or equal to 1." });
            }

            if (pageSize.HasValue && (pageSize.Value <= 0 || pageSize.Value > 100))
            {
                return Results.BadRequest(new { message = "Page size must be between 1 and 100." });
            }

            var pageNumber = page ?? 1;
            var size = pageSize ?? 20;

            try
            {
                var history = await service.GetPaymentHistoryAsync(
                    customerId,
                    planType,
                    planId,
                    status,
                    startDate,
                    endDate,
                    search,
                    pageNumber,
                    size,
                    ct);

                return Results.Ok(history);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("CustomerGetPaymentHistory")
        .WithSummary("Unified customer payment history")
        .WithDescription("Retrieves paginated unified financial transactions across both Chitu and Jewellery plans with filtering by plan type, status, and dates.")
        .Produces<CustomerPaymentsPagedResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // 2. GET /api/v1/customer/payments/{paymentId}
        group.MapGet("/payments/{paymentId:guid}", async (
            Guid paymentId,
            ClaimsPrincipal user,
            ICustomerPaymentService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var detail = await service.GetPaymentDetailAsync(customerId, paymentId, ct);
                return Results.Ok(detail);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("CustomerGetPaymentDetail")
        .WithSummary("Get payment transaction details")
        .WithDescription("Retrieves single receipt details including slot breakdowns (for Chitu) or gold grams and historical rate (for Jewellery).")
        .Produces<CustomerPaymentDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static bool TryGetCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(idStr, out customerId);
    }
}
