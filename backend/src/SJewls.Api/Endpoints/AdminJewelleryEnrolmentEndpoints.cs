using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class AdminJewelleryEnrolmentEndpoints
{
    public static void MapAdminJewelleryEnrolmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/jewellery-enrolments")
            .WithTags("Admin Jewellery Enrolments")
            .RequireAuthorization("StaffOnly");

        // 1. POST /api/v1/admin/jewellery-enrolments/preview-assignment
        group.MapPost("/preview-assignment", async (
            ClaimsPrincipal user,
            IAdminJewelleryEnrolmentService service,
            [FromBody] AdminEnrolmentPreviewRequest request,
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
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("AdminPreviewPlanAssignment")
        .WithSummary("Preview Plan Assignment Eligibility and Repeat Status")
        .WithDescription("Checks customer and plan eligibility, verifies repeat enrolment count, and provides read-only target grams, premium, and offered durations.")
        .Produces<AdminEnrolmentPreviewResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // 2. POST /api/v1/admin/jewellery-enrolments
        group.MapPost("/", async (
            ClaimsPrincipal user,
            IAdminJewelleryEnrolmentService service,
            [FromBody] AdminAssignPlanRequest request,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var enrolment = await service.AssignPlanAsync(staffId.Value, request, ip, ct);
                return Results.Created($"/api/v1/admin/jewellery-enrolments/{enrolment.Id}", enrolment);
            }
            catch (RepeatEnrolmentConfirmationRequiredException ex)
            {
                return Results.Json(new
                {
                    requiresConfirmation = true,
                    code = ex.Code,
                    totalEnrolmentCount = ex.TotalEnrolmentCount,
                    activeEnrolmentCount = ex.ActiveEnrolmentCount,
                    message = ex.Message
                }, statusCode: StatusCodes.Status409Conflict);
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
                return Results.Conflict(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("AdminAssignJewelleryPlan")
        .WithSummary("Assign Jewellery Savings Plan to Customer by Staff")
        .WithDescription("Creates a new active enrolment for an authorized customer without initial payment. Enforces read-only plan target grams, offered durations, branch eligibility, and repeat enrolment confirmation.")
        .Produces<AdminCustomerEnrolmentDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // 3. GET /api/v1/admin/jewellery-enrolments/{enrolmentId:guid}
        group.MapGet("/{enrolmentId:guid}", async (
            Guid enrolmentId,
            ClaimsPrincipal user,
            IAdminJewelleryEnrolmentService service,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var enrolment = await service.GetEnrolmentByIdAsync(staffId.Value, enrolmentId, ct);
                return Results.Ok(enrolment);
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
        .WithName("AdminGetJewelleryEnrolmentById")
        .WithSummary("Get Single Jewellery Enrolment Details")
        .WithDescription("Retrieves detailed snapshot terms, saved and remaining grams, progress, and contributions for a specific enrolment.")
        .Produces<AdminCustomerEnrolmentDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // 4. POST /api/v1/admin/jewellery-enrolments/{enrolmentId:guid}/payments/preview
        group.MapPost("/{enrolmentId:guid}/payments/preview", async (
            Guid enrolmentId,
            ClaimsPrincipal user,
            IAdminJewelleryEnrolmentService service,
            [FromBody] AdminRecordPaymentPreviewRequest request,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var preview = await service.PreviewPaymentAsync(staffId.Value, enrolmentId, request, ct);
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
                return Results.Conflict(new { message = ex.Message });
            }
            catch (GoldRateUnavailableException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("AdminPreviewInStorePayment")
        .WithSummary("Preview In-Store Cash Payment Calculation")
        .WithDescription("Calculates authoritative Buy Amount or Buy Gram breakdown using live 24K gold rate and enrolment premium snapshot without altering database state.")
        .Produces<AdminRecordPaymentPreviewResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        // 5. POST /api/v1/admin/jewellery-enrolments/{enrolmentId:guid}/payments
        group.MapPost("/{enrolmentId:guid}/payments", async (
            Guid enrolmentId,
            ClaimsPrincipal user,
            IAdminJewelleryEnrolmentService service,
            [FromBody] AdminRecordPaymentRequest request,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var result = await service.RecordPaymentAsync(staffId.Value, enrolmentId, request, ip, ct);
                return Results.Ok(result);
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
                return Results.Conflict(new { message = ex.Message });
            }
            catch (GoldRateUnavailableException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("AdminRecordInStorePayment")
        .WithSummary("Record In-Store Cash Received for Enrolment")
        .WithDescription("Authoritatively records cash received in store on today's branch date. Applies live gold rate, credits gold grams atomically, updates balances, and marks enrolment Completed if target is reached.")
        .Produces<AdminRecordPaymentResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // 6. GET /api/v1/admin/jewellery-enrolments/{enrolmentId:guid}/payments
        group.MapGet("/{enrolmentId:guid}/payments", async (
            Guid enrolmentId,
            ClaimsPrincipal user,
            IAdminJewelleryEnrolmentService service,
            CancellationToken ct) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var payments = await service.GetEnrolmentPaymentsAsync(staffId.Value, enrolmentId, ct);
                return Results.Ok(payments);
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
        .WithName("AdminGetJewelleryEnrolmentPayments")
        .WithSummary("Get Payment History for Enrolment")
        .WithDescription("Retrieves all confirmed payments and gold contributions for a specific enrolment.")
        .Produces<List<EnrolmentContributionDto>>(StatusCodes.Status200OK)
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
