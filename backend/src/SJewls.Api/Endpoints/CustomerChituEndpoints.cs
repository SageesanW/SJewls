using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class CustomerChituEndpoints
{
    public static IEndpointRouteBuilder MapCustomerChituEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customer")
            .RequireAuthorization()
            .WithTags("Customer Chitu Plans");

        #region Discovery

        // 1. GET /api/v1/customer/chitu-plans
        group.MapGet("/chitu-plans", async (
            ClaimsPrincipal user,
            ICustomerChituService service,
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
                var response = await service.GetAvailablePlansAsync(customerId, pageNumber, size, ct);
                return Results.Ok(response);
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
        .WithName("CustomerGetAvailableChituPlans")
        .WithSummary("Browse active and available Chitu plans")
        .WithDescription("Retrieves paginated Chitu plans open for joining or late-joining in the customer's registered branch.")
        .Produces<CustomerChituPlansPagedResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // 2. GET /api/v1/customer/chitu-plans/{planId}
        group.MapGet("/chitu-plans/{planId:guid}", async (
            Guid planId,
            ClaimsPrincipal user,
            ICustomerChituService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var detail = await service.GetPlanDetailsAsync(customerId, planId, ct);
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
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("CustomerGetChituPlanDetails")
        .WithSummary("Get Chitu plan details with schedule and terms")
        .WithDescription("Retrieves full details, round schedule, and terms for a specific Chitu plan.")
        .Produces<CustomerChituPlanDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        #endregion

        #region Joining & Purchasing Slots

        // 3. POST /api/v1/customer/chitu-plans/{planId}/join-preview
        group.MapPost("/chitu-plans/{planId:guid}/join-preview", async (
            Guid planId,
            [FromBody] CustomerChituJoinPreviewRequest request,
            ClaimsPrincipal user,
            ICustomerChituService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var preview = await service.PreviewJoinAsync(customerId, planId, request, ct);
                return Results.Ok(preview);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("CustomerPreviewChituJoin")
        .WithSummary("Preview initial payment for joining a Chitu plan")
        .WithDescription("Calculates required initial payment covering Round 1 (pre-enrolment) or all elapsed rounds (late joining) for requested slot quantity.")
        .Produces<CustomerChituJoinPreviewResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 4. POST /api/v1/customer/chitu-plans/{planId}/join
        group.MapPost("/chitu-plans/{planId:guid}/join", async (
            Guid planId,
            [FromBody] CustomerChituJoinRequest request,
            [FromHeader(Name = "X-Idempotency-Key")] string? headerIdempotencyKey,
            HttpContext httpContext,
            ClaimsPrincipal user,
            ICustomerChituService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.IdempotencyKey) && !string.IsNullOrWhiteSpace(headerIdempotencyKey))
            {
                request.IdempotencyKey = headerIdempotencyKey;
            }

            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var response = await service.ConfirmAndJoinAsync(customerId, planId, request, ipAddress, ct);
                return Results.Created($"/api/v1/customer/chitu-investments/{planId}", response);
            }
            catch (PaymentProviderNotConfiguredException ex)
            {
                return Results.Json(new { code = ex.Code, message = ex.Message }, statusCode: StatusCodes.Status501NotImplemented);
            }
            catch (ChituCapacityUnavailableException ex)
            {
                return Results.Conflict(new
                {
                    code = ex.Code,
                    message = ex.Message
                });
            }
            catch (ChituIdempotencyConflictException ex)
            {
                return Results.Conflict(new
                {
                    code = ex.Code,
                    message = ex.Message
                });
            }
            catch (ChituPreviewExpiredOrChangedException ex)
            {
                return Results.Conflict(new
                {
                    code = ex.Code,
                    message = ex.Message,
                    expectedAmount = ex.ExpectedAmount,
                    actualAmount = ex.ActualAmount
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("Capacity", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Conflict(new { code = ChituCapacityUnavailableException.ErrorCode, message = ex.Message });
                }
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("CustomerConfirmChituJoin")
        .WithSummary("Confirm and join Chitu plan with slot purchase")
        .WithDescription("Idempotently processes initial payment (or simulated payment in test mode) and assigns sequential permanent slots.")
        .Produces<CustomerChituJoinResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status501NotImplemented);

        #endregion

        #region My Plans (Portfolio)

        // 5. GET /api/v1/customer/chitu-investments
        group.MapGet("/chitu-investments", async (
            ClaimsPrincipal user,
            ICustomerChituService service,
            [FromQuery] string? status,
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
                var response = await service.GetInvestmentsAsync(customerId, status, pageNumber, size, ct);
                return Results.Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("CustomerGetChituInvestments")
        .WithSummary("Get customer's Chitu portfolio (My Plans)")
        .WithDescription("Returns paginated Chitu plans owned by the customer, grouped by plan, with individual slot breakdowns, arrears, and eligibility.")
        .Produces<CustomerChituInvestmentsPagedResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Alias: GET /api/v1/customer/my-plans
        group.MapGet("/my-plans", async (
            ClaimsPrincipal user,
            ICustomerChituService service,
            [FromQuery] string? status,
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

            var response = await service.GetInvestmentsAsync(customerId, status, pageNumber, size, ct);
            return Results.Ok(response);
        })
        .WithName("CustomerGetMyPlansAlias")
        .WithSummary("Alias for Customer Chitu investments (My Plans)")
        .Produces<CustomerChituInvestmentsPagedResponse>(StatusCodes.Status200OK);

        // 6. GET /api/v1/customer/chitu-investments/{planId}
        group.MapGet("/chitu-investments/{planId:guid}", async (
            Guid planId,
            ClaimsPrincipal user,
            ICustomerChituService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var planDetail = await service.GetInvestmentPlanDetailsAsync(customerId, planId, ct);
                return Results.Ok(planDetail);
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
        .WithName("CustomerGetChituInvestmentPlanDetails")
        .WithSummary("Get single Chitu plan portfolio details")
        .Produces<CustomerChituInvestmentPlanDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 7. GET /api/v1/customer/chitu-slots/{slotId}
        group.MapGet("/chitu-slots/{slotId:guid}", async (
            Guid slotId,
            ClaimsPrincipal user,
            ICustomerChituService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var slotDetail = await service.GetSlotDetailsAsync(customerId, slotId, ct);
                return Results.Ok(slotDetail);
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
        .WithName("CustomerGetChituSlotDetails")
        .WithSummary("Get detailed information for an individual slot")
        .WithDescription("Retrieves slot instalment schedule, payment statuses, arrears, and winner entitlement claim data.")
        .Produces<CustomerChituSlotDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 8. GET /api/v1/customer/chitu-slots/{slotId}/payments
        group.MapGet("/chitu-slots/{slotId:guid}/payments", async (
            Guid slotId,
            ClaimsPrincipal user,
            ICustomerChituService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var payments = await service.GetSlotPaymentsAsync(customerId, slotId, ct);
                return Results.Ok(payments);
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
        .WithName("CustomerGetChituSlotPayments")
        .WithSummary("Get payment history for a specific slot")
        .Produces<List<CustomerPaymentSummaryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        #endregion

        #region Existing-Slot Contributions

        // 9. GET /api/v1/customer/chitu-plans/{planId}/payable-slots
        group.MapGet("/chitu-plans/{planId:guid}/payable-slots", async (
            Guid planId,
            ClaimsPrincipal user,
            ICustomerChituService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var payableSlots = await service.GetPayableSlotsAsync(customerId, planId, ct);
                return Results.Ok(payableSlots);
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
        .WithName("CustomerGetPayableSlots")
        .WithSummary("Get customer's payable slots for a Chitu plan")
        .WithDescription("Returns all slots owned by the customer in this plan with payment eligibility, outstanding due, and unpaid rounds.")
        .Produces<CustomerPayableSlotsDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 10. POST /api/v1/customer/chitu-plans/{planId}/payments/preview
        group.MapPost("/chitu-plans/{planId:guid}/payments/preview", async (
            Guid planId,
            [FromBody] CustomerChituPaymentPreviewRequest request,
            ClaimsPrincipal user,
            ICustomerChituService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var preview = await service.PreviewExistingSlotPaymentAsync(customerId, planId, request, ct);
                return Results.Ok(preview);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("CustomerPreviewChituExistingSlotPayment")
        .WithSummary("Preview payment allocation for existing slots")
        .WithDescription("Calculates per-slot oldest-unpaid-first allocations, resulting arrears, and raffle eligibility.")
        .Produces<CustomerChituPaymentPreviewResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 11. POST /api/v1/customer/chitu-plans/{planId}/payments
        group.MapPost("/chitu-plans/{planId:guid}/payments", async (
            Guid planId,
            [FromBody] CustomerChituPaymentConfirmRequest request,
            [FromHeader(Name = "X-Idempotency-Key")] string? headerIdempotencyKey,
            HttpContext httpContext,
            ClaimsPrincipal user,
            ICustomerChituService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.IdempotencyKey) && !string.IsNullOrWhiteSpace(headerIdempotencyKey))
            {
                request.IdempotencyKey = headerIdempotencyKey;
            }

            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var response = await service.ConfirmExistingSlotPaymentAsync(customerId, planId, request, ipAddress, ct);
                return Results.Ok(response);
            }
            catch (PaymentProviderNotConfiguredException ex)
            {
                return Results.Json(new { code = ex.Code, message = ex.Message }, statusCode: StatusCodes.Status501NotImplemented);
            }
            catch (ChituIdempotencyConflictException ex)
            {
                return Results.Conflict(new
                {
                    code = ex.Code,
                    message = ex.Message
                });
            }
            catch (ChituPreviewExpiredOrChangedException ex)
            {
                return Results.Conflict(new
                {
                    code = ex.Code,
                    message = ex.Message,
                    expectedAmount = ex.ExpectedAmount,
                    actualAmount = ex.ActualAmount
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("CustomerConfirmChituExistingSlotPayment")
        .WithSummary("Confirm and post payment for existing slots")
        .WithDescription("Idempotently processes contribution payment, marks instalments as Paid, and updates slot coverage.")
        .Produces<CustomerChituPaymentConfirmResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status501NotImplemented);

        #endregion

        return app;
    }

    private static bool TryGetCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(idStr, out customerId);
    }
}
