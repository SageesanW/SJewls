using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class CustomerJewelleryPlanEndpoints
{
    public static IEndpointRouteBuilder MapCustomerJewelleryPlanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customer")
            .WithTags("Customer Jewellery Plans & Enrolments")
            .RequireAuthorization();

        #region Category & Plan Browsing

        // 1. GET /api/v1/customer/jewellery-categories
        group.MapGet("/jewellery-categories", async (
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var categories = await service.GetCategoriesAsync(customerId, ct);
                return Results.Ok(categories);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("CustomerGetJewelleryCategories")
        .WithSummary("Browse active jewellery categories")
        .WithDescription("Retrieves active jewellery categories for the customer's registered branch, including counts of currently available plans.")
        .Produces<List<CustomerJewelleryCategoryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // 2. GET /api/v1/customer/jewellery-categories/{categoryId}/plans
        group.MapGet("/jewellery-categories/{categoryId:guid}/plans", async (
            Guid categoryId,
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var plans = await service.GetPlansByCategoryAsync(customerId, categoryId, ct);
                return Results.Ok(plans);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("CustomerGetPlansByCategory")
        .WithSummary("Browse available plans in category")
        .WithDescription("Returns currently available jewellery plans for a specific category. Plans whose admin start date is in the future are strictly excluded.")
        .Produces<List<CustomerJewelleryPlanDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // 3. GET /api/v1/customer/jewellery-plans (optionally ?categoryId=...)
        group.MapGet("/jewellery-plans", async (
            [FromQuery] Guid? categoryId,
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var plans = await service.GetPlansAsync(customerId, categoryId, ct);
                return Results.Ok(plans);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("CustomerGetJewelleryPlans")
        .WithSummary("Browse available jewellery plans")
        .WithDescription("Retrieves active, currently available jewellery plans for the customer's branch, optionally filtered by category.")
        .Produces<List<CustomerJewelleryPlanDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // 4. GET /api/v1/customer/jewellery-plans/{planId}
        group.MapGet("/jewellery-plans/{planId:guid}", async (
            Guid planId,
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var plan = await service.GetPlanDetailsAsync(customerId, planId, ct);
                return Results.Ok(plan);
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
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("CustomerGetPlanDetails")
        .WithSummary("Get jewellery plan details")
        .WithDescription("Retrieves details of an individual plan including target gold grams, offered durations, and premium percentage.")
        .Produces<CustomerJewelleryPlanDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        #endregion

        #region Enrolment Confirmation & Creation

        // 4. POST /api/v1/customer/jewellery-enrolments/preview
        group.MapPost("/jewellery-enrolments/preview", async (
            [FromBody] EnrolmentPreviewRequest request,
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var summary = await service.PreviewEnrolmentAsync(customerId, request, ct);
                return Results.Ok(summary);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        })
        .WithName("CustomerPreviewEnrolment")
        .WithSummary("Preview enrolment confirmation summary")
        .WithDescription("Returns a proposed enrolment confirmation summary containing plan snapshot, target grams, premium percentage, proposed joining date and deadline.")
        .Produces<EnrolmentPreviewResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // 5. POST /api/v1/customer/jewellery-enrolments
        group.MapPost("/jewellery-enrolments", async (
            [FromBody] CreateCustomerEnrolmentRequest request,
            [FromHeader(Name = "X-Idempotency-Key")] string? headerIdempotencyKey,
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            // Honor header or body idempotency key
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey) && !string.IsNullOrWhiteSpace(headerIdempotencyKey))
            {
                request.IdempotencyKey = headerIdempotencyKey;
            }

            try
            {
                var enrolment = await service.CreateEnrolmentAsync(customerId, request, ct);
                return Results.Created($"/api/v1/customer/jewellery-enrolments/{enrolment.EnrolmentId}", enrolment);
            }
            catch (RepeatEnrolmentConfirmationRequiredException ex)
            {
                return Results.Conflict(new RepeatEnrolmentConflictResponse
                {
                    Code = ex.Code,
                    Message = ex.Message,
                    TotalEnrolments = ex.TotalEnrolmentCount,
                    ActiveEnrolments = ex.ActiveEnrolmentCount,
                    TotalEnrolmentCount = ex.TotalEnrolmentCount,
                    ActiveEnrolmentCount = ex.ActiveEnrolmentCount
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
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        })
        .WithName("CustomerCreateEnrolment")
        .WithSummary("Confirm and join jewellery plan")
        .WithDescription("Creates an active enrolment immediately without requiring an initial payment. Idempotent across retries with the same IdempotencyKey. Triggers an activation notification. Requires explicit confirmation if the customer has previously enrolled in this plan.")
        .Produces<CustomerEnrolmentDto>(StatusCodes.Status201Created)
        .Produces<RepeatEnrolmentConflictResponse>(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        #endregion

        #region My Investments (Enrolments)

        // 6. GET /api/v1/customer/jewellery-enrolments
        group.MapGet("/jewellery-enrolments", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            var safePage = page > 0 ? page : 1;
            var safePageSize = pageSize > 0 && pageSize <= 50 ? pageSize : 20;

            try
            {
                var result = await service.GetEnrolmentsAsync(customerId, safePage, safePageSize, ct);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("CustomerGetEnrolments")
        .WithSummary("List customer enrolments (My Investments)")
        .WithDescription("Retrieves paginated jewellery plan enrolments for the authenticated customer, showing target grams, net accumulated gold, remaining grams, and contribution availability.")
        .Produces<CustomerEnrolmentsPagedResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // 7. GET /api/v1/customer/jewellery-enrolments/{enrolmentId}
        group.MapGet("/jewellery-enrolments/{enrolmentId:guid}", async (
            Guid enrolmentId,
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var enrolment = await service.GetEnrolmentDetailsAsync(customerId, enrolmentId, ct);
                return Results.Ok(enrolment);
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
        .WithName("CustomerGetEnrolmentDetails")
        .WithSummary("Get enrolment details")
        .WithDescription("Retrieves full details and progress metrics for a specific enrolment owned by the customer.")
        .Produces<CustomerEnrolmentDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 8. GET /api/v1/customer/jewellery-enrolments/{enrolmentId}/payments
        group.MapGet("/jewellery-enrolments/{enrolmentId:guid}/payments", async (
            Guid enrolmentId,
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var payments = await service.GetEnrolmentPaymentsAsync(customerId, enrolmentId, ct);
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
        .WithName("CustomerGetEnrolmentPayments")
        .WithSummary("List enrolment contribution history")
        .WithDescription("Retrieves the confirmed contribution and payment ledger history for this jewellery plan enrolment.")
        .Produces<List<CustomerEnrolmentPaymentDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        #endregion

        #region Previews, Locked Quotes & Payment Foundation

        // 9. POST /api/v1/customer/jewellery-enrolments/{enrolmentId}/preview
        group.MapPost("/jewellery-enrolments/{enrolmentId:guid}/preview", async (
            Guid enrolmentId,
            [FromBody] JewelleryCalculationRequest request,
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var preview = await service.PreviewCalculationAsync(customerId, enrolmentId, request, ct);
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
            catch (GoldRateUnavailableException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        })
        .WithName("CustomerPreviewPaymentCalculation")
        .WithSummary("Preview gold payment calculation breakdown")
        .WithDescription("Calculates an informational calculation preview for Buy Gram or Buy Amount mode without altering balances or creating a quote.")
        .Produces<JewelleryCalculationPreviewResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // 10. POST /api/v1/customer/jewellery-enrolments/{enrolmentId}/quotes
        group.MapPost("/jewellery-enrolments/{enrolmentId:guid}/quotes", async (
            Guid enrolmentId,
            [FromBody] JewelleryCalculationRequest request,
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var quote = await service.CreateLockedQuoteAsync(customerId, enrolmentId, request, ct);
                return Results.Created($"/api/v1/customer/jewellery-quotes/{quote.QuoteId}", quote);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (GoldRateUnavailableException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        })
        .WithName("CustomerCreatePaymentQuote")
        .WithSummary("Lock time-limited payment quote (Pay Now)")
        .WithDescription("Locks an authoritative payment quote for 5 minutes at the latest applicable gold rate. Guarantees payable amount and credited grams during validity.")
        .Produces<CustomerPaymentQuoteDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // 11. GET /api/v1/customer/jewellery-quotes/{quoteId}
        group.MapGet("/jewellery-quotes/{quoteId:guid}", async (
            Guid quoteId,
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var quote = await service.GetQuoteAsync(customerId, quoteId, ct);
                return Results.Ok(quote);
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
        .WithName("CustomerGetPaymentQuote")
        .WithSummary("Get payment quote status")
        .WithDescription("Retrieves the status, values, and expiry of a locked payment quote.")
        .Produces<CustomerPaymentQuoteDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 12. POST /api/v1/customer/jewellery-quotes/{quoteId}/pay
        group.MapPost("/jewellery-quotes/{quoteId:guid}/pay", async (
            Guid quoteId,
            ClaimsPrincipal user,
            ICustomerJewelleryPlanService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var response = await service.InitiatePaymentAsync(customerId, quoteId, ct);
                // Return 501 Not Implemented because online payment gateway integration is deferred
                return Results.Json(response, statusCode: StatusCodes.Status501NotImplemented);
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
                return Results.Conflict(new { message = ex.Message });
            }
        })
        .WithName("CustomerInitiatePayment")
        .WithSummary("Initiate payment for locked quote (Gateway Deferred)")
        .WithDescription("Gateway integration is deferred. Returns PaymentProviderNotConfigured (HTTP 501). No fake checkout URL is generated, and balances remain unchanged.")
        .Produces<PaymentInitiationResponse>(StatusCodes.Status501NotImplemented)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        #endregion

        #region Customer Notifications

        // 13. GET /api/v1/customer/notifications
        group.MapGet("/notifications", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            ClaimsPrincipal user,
            ICustomerNotificationService notificationService,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            var safePage = page > 0 ? page : 1;
            var safePageSize = pageSize > 0 && pageSize <= 50 ? pageSize : 20;

            var result = await notificationService.GetNotificationsAsync(customerId, safePage, safePageSize, ct);
            return Results.Ok(result);
        })
        .WithName("CustomerGetNotifications")
        .WithSummary("Get customer notifications")
        .WithDescription("Retrieves paginated notifications for the customer (such as jewellery plan activations and completions).")
        .Produces<CustomerNotificationsPagedResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // 14. POST /api/v1/customer/notifications/{notificationId}/read
        group.MapPost("/notifications/{notificationId:guid}/read", async (
            Guid notificationId,
            ClaimsPrincipal user,
            ICustomerNotificationService notificationService,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            var success = await notificationService.MarkAsReadAsync(customerId, notificationId, ct);
            if (!success)
            {
                return Results.NotFound(new { message = "Notification not found." });
            }
            return Results.Ok(new { message = "Notification marked as read." });
        })
        .WithName("CustomerMarkNotificationAsRead")
        .WithSummary("Mark notification as read")
        .WithDescription("Marks an individual customer notification as read.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 15. POST /api/v1/customer/notifications/read-all
        group.MapPost("/notifications/read-all", async (
            ClaimsPrincipal user,
            ICustomerNotificationService notificationService,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            var count = await notificationService.MarkAllAsReadAsync(customerId, ct);
            return Results.Ok(new { message = $"{count} notifications marked as read." });
        })
        .WithName("CustomerMarkAllNotificationsAsRead")
        .WithSummary("Mark all notifications as read")
        .WithDescription("Marks all unread notifications for the customer as read.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        #endregion

        return app;
    }

    private static bool TryGetCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(idStr, out customerId);
    }
}
