using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class JewelleryPlanEndpoints
{
    public static void MapJewelleryPlanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/jewellery-plans")
            .WithTags("Admin Jewellery Plans");

        // 1. GET /api/v1/admin/jewellery-plans
        group.MapGet("/", async (
            ClaimsPrincipal user,
            IJewelleryPlanService service,
            [FromQuery] string? search,
            [FromQuery] Guid? categoryId,
            [FromQuery] string? status,
            [FromQuery] Guid? branchId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var result = await service.GetPlansAsync(
                    staffId.Value, search, categoryId, status, branchId, page, pageSize, ct);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireAuthorization("StaffOnly")
        .WithName("AdminGetJewelleryPlans")
        .WithSummary("List Jewellery Plans with Card Progress")
        .WithDescription("Retrieves a paginated card grid of jewellery plans with database-derived overall gold progress, category filtering, search, and status (Active, Scheduled, Deactivated). Branch Staff are strictly isolated to their branch; Super Admins can filter across branches.")
        .Produces<JewelleryPlanPagedResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 2. GET /api/v1/admin/jewellery-plans/{id}
        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IJewelleryPlanService service,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var plan = await service.GetPlanByIdAsync(id, staffId.Value, ct);
                if (plan == null)
                {
                    return Results.NotFound(new { message = $"Jewellery plan with ID '{id}' was not found." });
                }
                return Results.Ok(plan);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireAuthorization("StaffOnly")
        .WithName("AdminGetJewelleryPlanById")
        .WithSummary("Get Jewellery Plan Details")
        .WithDescription("Retrieves detailed information for a specific jewellery plan, including duration options, target gold grams, start date, and enrolment progress.")
        .Produces<JewelleryPlanDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 3. POST /api/v1/admin/jewellery-plans
        group.MapPost("/", async (
            ClaimsPrincipal user,
            IJewelleryPlanService service,
            [FromBody] CreateJewelleryPlanRequest request,
            HttpContext httpContext,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var created = await service.CreatePlanAsync(request, staffId.Value, ip, ct);
                return Results.Created($"/api/v1/admin/jewellery-plans/{created.Id}", created);
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
        .RequireAuthorization("BranchAdminOrSuperAdmin")
        .WithName("AdminCreateJewelleryPlan")
        .WithSummary("Create Jewellery Plan")
        .WithDescription("Creates a new jewellery savings plan with selected category, custom or default duration options (in whole months), authoritative target gold grams, and start date. Validates category branch ownership.")
        .Produces<JewelleryPlanDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 4. PUT /api/v1/admin/jewellery-plans/{id}
        group.MapPut("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IJewelleryPlanService service,
            [FromBody] UpdateJewelleryPlanRequest request,
            HttpContext httpContext,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var updated = await service.UpdatePlanAsync(id, request, staffId.Value, ip, ct);
                return Results.Ok(updated);
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
        .RequireAuthorization("BranchAdminOrSuperAdmin")
        .WithName("AdminUpdateJewelleryPlan")
        .WithSummary("Update Jewellery Plan")
        .WithDescription("Updates plan metadata, category, target gold grams, start date, and offered durations. Existing customer enrolments retain their agreed snapshot terms (target grams, selected duration, joining date, and deadline).")
        .Produces<JewelleryPlanDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 5. PATCH /api/v1/admin/jewellery-plans/{id}/status
        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            ClaimsPrincipal user,
            IJewelleryPlanService service,
            [FromBody] UpdateJewelleryPlanStatusRequest request,
            HttpContext httpContext,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var updated = await service.UpdatePlanStatusAsync(id, request, staffId.Value, ip, ct);
                return Results.Ok(updated);
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
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireAuthorization("BranchAdminOrSuperAdmin")
        .WithName("AdminUpdateJewelleryPlanStatus")
        .WithSummary("Deactivate or Reopen Jewellery Plan")
        .WithDescription("Transitions plan operational status between Active/Scheduled and Deactivated. Deactivating blocks new enrolments while allowing existing enrolled customers to continue contributing until their duration deadline. Reopening makes the plan available again once the start date arrives. Plan deletion is prohibited.")
        .Produces<JewelleryPlanDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 6. GET /api/v1/admin/jewellery-plans/{id}/customers
        group.MapGet("/{id:guid}/customers", async (
            Guid id,
            ClaimsPrincipal user,
            IJewelleryPlanService service,
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var result = await service.GetPlanCustomersAsync(id, staffId.Value, search, status, page, pageSize, ct);
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
        .RequireAuthorization("StaffOnly")
        .WithName("AdminGetJewelleryPlanCustomers")
        .WithSummary("View Enrolled Customers for Jewellery Plan")
        .WithDescription("Retrieves a paginated list of customers enrolled in the specified jewellery plan, showing joining date, chosen duration, deadline, enrolment target grams, confirmed currency paid, net accumulated grams, remaining balance, individual progress percentage, and contribution history.")
        .Produces<PlanCustomersPagedResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 7. POST /api/v1/admin/jewellery-plans/upload-image
        group.MapPost("/upload-image", async (
            IFormFile file,
            ISupabaseStorageService storageService,
            CancellationToken ct = default) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "No jewellery image file provided." });
            }

            if (!storageService.ValidateImageFile(file.FileName, file.ContentType, file.Length, out var validationError))
            {
                return Results.BadRequest(new { message = validationError });
            }

            try
            {
                using var stream = file.OpenReadStream();
                var publicUrl = await storageService.UploadImageAsync(stream, file.FileName, file.ContentType, "jewellery-plans", ct);
                return Results.Ok(new ImageUploadResponse
                {
                    Url = publicUrl,
                    FileName = file.FileName,
                    Size = file.Length
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .RequireAuthorization("BranchAdminOrSuperAdmin")
        .DisableAntiforgery()
        .WithName("AdminUploadJewelleryPlanImage")
        .WithSummary("Upload Jewellery Plan Visual Image")
        .WithDescription("Uploads a jewellery product image (PNG, JPG, WEBP, max 5MB) to Supabase Storage and returns the public CDN URL.")
        .Produces<ImageUploadResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 8. POST /api/v1/admin/jewellery-plans/financial-quote
        group.MapPost("/financial-quote", async (
            ClaimsPrincipal user,
            IJewelleryPlanService service,
            [FromBody] FinancialQuoteRequest request,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var quote = await service.CalculateFinancialQuoteAsync(request, staffId.Value, ct);
                return Results.Ok(quote);
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
        .RequireAuthorization("StaffOnly")
        .WithName("AdminCalculateJewelleryFinancialQuote")
        .WithSummary("Calculate Gold Rate Financial Quote")
        .WithDescription("Calculates gold contribution conversion using the current applicable 24K branch rate. Supports conversion by grams (grams × rate = payable money) and by money (money ÷ rate = credited grams) with strict 4-decimal gram precision and remainder checks.")
        .Produces<FinancialQuoteResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    private static Guid? GetStaffId(ClaimsPrincipal user)
    {
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
