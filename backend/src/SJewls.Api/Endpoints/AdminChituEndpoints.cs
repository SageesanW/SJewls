using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class AdminChituEndpoints
{
    public static void MapAdminChituEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/chitu-plans")
            .WithTags("Admin Chitu Plans")
            .RequireAuthorization("StaffOnly");

        // 1. GET /api/v1/admin/chitu-plans
        group.MapGet("/", async (
            ClaimsPrincipal user,
            IChituService service,
            [FromQuery] Guid? branchId,
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] int? redemptionType,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var result = await service.GetPlansAsync(staffId.Value, branchId, search, status, redemptionType, ct);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("AdminGetChituPlans")
        .WithSummary("List Chitu Plans with Database-Derived Live Card Metrics")
        .WithDescription("Retrieves active, inactive, or all Chitu plans with real-time slot payment coverage and overdue raffle indicators.")
        .Produces<ChituPlanListResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 2. POST /api/v1/admin/chitu-plans
        group.MapPost("/", async (
            ClaimsPrincipal user,
            IChituService service,
            [FromBody] CreateChituPlanRequest request,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var result = await service.CreatePlanAsync(staffId.Value, request, ct);
                return Results.Created($"/api/v1/admin/chitu-plans/{result.Id}", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
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
        .WithName("AdminCreateChituPlan")
        .WithSummary("Create a New Chitu Plan")
        .WithDescription("Creates a new Cash or Gold Chitu savings plan with scheduled raffle rounds and calculated end date.")
        .Produces<ChituPlanCardDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // 3. GET /api/v1/admin/chitu-plans/{id}
        group.MapGet("/{id:guid}", async (
            ClaimsPrincipal user,
            IChituService service,
            Guid id,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var result = await service.GetPlanByIdAsync(staffId.Value, id, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("AdminGetChituPlanById")
        .WithSummary("Get Detailed Chitu Plan Information and Schedule")
        .WithDescription("Fetches plan details including the full raffle round schedule and winner history.")
        .Produces<ChituPlanDetailDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // 4. PUT /api/v1/admin/chitu-plans/{id}
        group.MapPut("/{id:guid}", async (
            ClaimsPrincipal user,
            IChituService service,
            Guid id,
            [FromBody] UpdateChituPlanRequest request,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var result = await service.UpdatePlanAsync(staffId.Value, id, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
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
        .WithName("AdminUpdateChituPlan")
        .WithSummary("Update Chitu Plan Descriptive Fields")
        .WithDescription("Allows safe descriptive updates to Chitu plan name, description, or status.")
        .Produces<ChituPlanCardDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // 5. POST /api/v1/admin/chitu-plans/preview-assignment (alias: /assignment/preview)
        var previewAssignmentDelegate = async (
            ClaimsPrincipal user,
            IChituService service,
            [FromBody] ChituSlotAssignmentPreviewRequest request,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var result = await service.PreviewAssignmentAsync(staffId.Value, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        };

        group.MapPost("/preview-assignment", previewAssignmentDelegate)
            .WithName("AdminPreviewChituSlotAssignment")
            .WithSummary("Preview Slot Assignment and Required Initial Payment")
            .WithDescription("Calculates next sequential slot numbers, required round coverage, and prepayment breakdowns before confirming assignment.")
            .Produces<ChituSlotAssignmentPreviewResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/assignment/preview", previewAssignmentDelegate)
            .ExcludeFromDescription();

        // 6. POST /api/v1/admin/chitu-plans/assign-slots (alias: /assignment)
        var assignSlotsDelegate = async (
            ClaimsPrincipal user,
            IChituService service,
            [FromBody] AssignChituSlotsRequest request,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var result = await service.AssignSlotsAsync(staffId.Value, request, ip, ct);
                return Results.Created("/api/v1/admin/chitu-plans/assign-slots", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        };

        group.MapPost("/assign-slots", assignSlotsDelegate)
            .WithName("AdminAssignChituSlots")
            .WithSummary("Assign Chitu Slots with Atomic Initial Payment")
            .WithDescription("Atomically creates sequentially numbered slots, marks initial scheduled instalments as paid, and posts confirmed payment to the ledger.")
            .Produces<AssignChituSlotsResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/assignment", assignSlotsDelegate)
            .ExcludeFromDescription();

        // 7. GET /api/v1/admin/chitu-plans/{id}/payable-slots/{customerId} (alias: /{id}/customers/{customerId}/payable-slots)
        var payableSlotsDelegate = async (
            ClaimsPrincipal user,
            IChituService service,
            Guid id,
            Guid customerId,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var result = await service.GetCustomerPayableSlotsAsync(staffId.Value, id, customerId, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        };

        group.MapGet("/{id:guid}/payable-slots/{customerId:guid}", payableSlotsDelegate)
            .WithName("AdminGetCustomerPayableSlots")
            .WithSummary("Get Customer's Payable Slots in a Chitu Plan")
            .WithDescription("Retrieves active slots owned by the customer with next unpaid instalments and confirmed paid totals.")
            .Produces<CustomerPayableSlotsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/customers/{customerId:guid}/payable-slots", payableSlotsDelegate)
            .ExcludeFromDescription();

        // 8. POST /api/v1/admin/chitu-plans/preview-payment (alias: /payments/preview)
        var previewPaymentDelegate = async (
            ClaimsPrincipal user,
            IChituService service,
            [FromBody] ChituPaymentPreviewRequest request,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var result = await service.PreviewPaymentAsync(staffId.Value, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        };

        group.MapPost("/preview-payment", previewPaymentDelegate)
            .WithName("AdminPreviewChituPayment")
            .WithSummary("Preview Payment Allocation Across Selected Slots")
            .WithDescription("Applies oldest-unpaid-first allocation per slot and verifies whole multiple amounts before confirmation.")
            .Produces<ChituPaymentPreviewResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/payments/preview", previewPaymentDelegate)
            .ExcludeFromDescription();

        // 9. POST /api/v1/admin/chitu-plans/record-payment (alias: /payments)
        var recordPaymentDelegate = async (
            ClaimsPrincipal user,
            IChituService service,
            [FromBody] RecordChituPaymentRequest request,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var result = await service.RecordPaymentAsync(staffId.Value, request, ip, ct);
                return Results.Created("/api/v1/admin/chitu-plans/record-payment", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        };

        group.MapPost("/record-payment", recordPaymentDelegate)
            .WithName("AdminRecordChituPayment")
            .WithSummary("Record In-Store Chitu Instalment Payment")
            .WithDescription("Atomically records confirmed cash, card, or bank transfer payments and posts allocations to oldest unpaid instalments.")
            .Produces<RecordChituPaymentResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/payments", recordPaymentDelegate)
            .ExcludeFromDescription();

        // 10. GET /api/v1/admin/chitu-plans/{id}/raffles/{roundIndex}/eligible-slots (alias: /{id}/rounds/{roundIndex}/eligible-slots)
        var eligibleSlotsDelegate = async (
            ClaimsPrincipal user,
            IChituService service,
            Guid id,
            int roundIndex,
            [FromQuery] string? search,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var result = await service.GetEligibleRaffleSlotsAsync(staffId.Value, id, roundIndex, search, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        };

        group.MapGet("/{id:guid}/raffles/{roundIndex:int}/eligible-slots", eligibleSlotsDelegate)
            .WithName("AdminGetEligibleRaffleSlots")
            .WithSummary("Get Slots Eligible for a Physical Raffle Round")
            .WithDescription("Retrieves active slots that have fully paid every instalment from Round 1 through the specified raffle round.")
            .Produces<ChituEligibleSlotsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/rounds/{roundIndex:int}/eligible-slots", eligibleSlotsDelegate)
            .ExcludeFromDescription();

        // 11. POST /api/v1/admin/chitu-plans/{id}/raffles/{roundIndex}/record (alias: /{id}/rounds/{roundIndex}/record-draw)
        var recordRaffleDelegate = async (
            ClaimsPrincipal user,
            IChituService service,
            Guid id,
            int roundIndex,
            [FromBody] RecordChituRaffleRequest request,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var result = await service.RecordRaffleDrawAsync(staffId.Value, id, roundIndex, request, ip, ct);
                return Results.Created($"/api/v1/admin/chitu-plans/{id}/raffles/{roundIndex}", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        };

        group.MapPost("/{id:guid}/raffles/{roundIndex:int}/record", recordRaffleDelegate)
            .WithName("AdminRecordChituRaffle")
            .WithSummary("Record the Physical Raffle Draw Winner for a Round")
            .WithDescription("Records the winning slot, stops future payment obligations, calculates entitlement (Price x Months), and flags advance excess for admin review.")
            .Produces<RecordChituRaffleResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/rounds/{roundIndex:int}/record-draw", recordRaffleDelegate)
            .ExcludeFromDescription();

        // 12. GET /api/v1/admin/chitu-plans/{id}/roster
        group.MapGet("/{id:guid}/roster", async (
            ClaimsPrincipal user,
            IChituService service,
            Guid id,
            [FromQuery] int? roundIndex,
            [FromQuery] string? search,
            [FromQuery] string? statusFilter,
            [FromQuery] string? paymentFilter,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var query = new ChituRosterQueryParameters
            {
                RoundIndex = roundIndex,
                Search = search,
                StatusFilter = statusFilter,
                PaymentFilter = paymentFilter,
                Page = page,
                PageSize = pageSize
            };

            try
            {
                var result = await service.GetRosterAsync(staffId.Value, id, query, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("AdminGetChituRoster")
        .WithSummary("Get Customer and Slot Roster for a Chitu Plan")
        .WithDescription("Displays slot-by-slot roster with paid/unpaid status for a selected round, paid-through month, total paid, and winner status.")
        .Produces<ChituRosterResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static Guid? GetStaffId(ClaimsPrincipal user)
    {
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
