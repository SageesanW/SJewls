using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class AdminCustomerEndpoints
{
    public static void MapAdminCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/customers")
            .WithTags("Admin Customers")
            .RequireAuthorization("StaffOnly");

        // 1. GET /api/v1/admin/customers
        group.MapGet("/", async (
            ClaimsPrincipal user,
            IAdminCustomerService customerService,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] Guid? branchId,
            [FromQuery] string? status,
            CancellationToken ct) =>
        {
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var currentStaffId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var result = await customerService.GetCustomersAsync(
                    currentStaffId,
                    page ?? 1,
                    pageSize ?? 10,
                    search,
                    branchId,
                    status,
                    ct);

                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("AdminGetCustomers")
        .WithSummary("List Customers with Pagination and Filters")
        .WithDescription("Retrieves a paginated list of customers. Supports search by name, phone, email, or NIC, as well as filtering by branch and account status. Super Admins can access all branches; Branch staff are strictly restricted to their assigned branch.")
        .Produces<AdminCustomerPagedResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 2. GET /api/v1/admin/customers/statistics
        group.MapGet("/statistics", async (
            ClaimsPrincipal user,
            IAdminCustomerService customerService,
            [FromQuery] string? search,
            [FromQuery] Guid? branchId,
            CancellationToken ct) =>
        {
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var currentStaffId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var statistics = await customerService.GetCustomerStatisticsAsync(currentStaffId, search, branchId, ct);
                return Results.Ok(statistics);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("AdminGetCustomerStatistics")
        .WithSummary("Customer Summary Card Statistics")
        .WithDescription("Calculates customer counts (Total, Active, Inactive) across matching branch and search criteria prior to pagination. Status filter is not applied so active and inactive counts remain independent.")
        .Produces<CustomerStatisticsDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 3. GET /api/v1/admin/customers/metrics
        group.MapGet("/metrics", async (
            ClaimsPrincipal user,
            IAdminCustomerService customerService,
            [FromQuery] Guid? branchId,
            CancellationToken ct) =>
        {
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var currentStaffId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var metrics = await customerService.GetCustomerMetricsAsync(currentStaffId, branchId, ct);
                return Results.Ok(metrics);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("AdminGetCustomerMetrics")
        .WithSummary("Customer and Slot Overview Metrics")
        .WithDescription("Provides aggregated counts for total slots, total customers, active investments, closed accounts, inactive accounts, and pending accounts according to staff branch permissions.")
        .Produces<AdminCustomerMetricsDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 4. GET /api/v1/admin/customers/{id:guid}
        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IAdminCustomerService customerService,
            CancellationToken ct) =>
        {
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var currentStaffId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var customer = await customerService.GetCustomerByIdAsync(currentStaffId, id, ct);
                return Results.Ok(customer);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { message = "Customer not found." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("AdminGetCustomerById")
        .WithSummary("Get Detailed Customer Profile")
        .WithDescription("Retrieves detailed profile information for an authorized customer, including date of birth, unmasked NIC for authorized staff, verification flags, and list of associated Chitu and Jewellery savings slots. Secrets, OTPs, and password hashes are never exposed.")
        .Produces<AdminCustomerDetailDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // 5. POST /api/v1/admin/customers
        group.MapPost("/", async (
            [FromBody] CreateCustomerRequest request,
            ClaimsPrincipal user,
            IAdminCustomerService customerService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var currentStaffId))
            {
                return Results.Unauthorized();
            }

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var customer = await customerService.CreateCustomerAsync(currentStaffId, request, ip, ct);
                return Results.Created($"/api/v1/admin/customers/{customer.Id}", customer);
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
        .WithName("AdminCreateCustomer")
        .WithSummary("Create Customer by Staff")
        .WithDescription("Allows authorized branch staff or Super Admin to create a customer. Normalizes inputs, prevents duplicate NICs and contacts, and keeps contacts unverified until customer verifies via OTP.")
        .Produces<AdminCustomerDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status403Forbidden);

        // 6. PATCH /api/v1/admin/customers/{id:guid}/status
        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            [FromBody] UpdateCustomerStatusRequest request,
            ClaimsPrincipal user,
            IAdminCustomerService customerService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var currentStaffId))
            {
                return Results.Unauthorized();
            }

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var customer = await customerService.UpdateCustomerStatusAsync(currentStaffId, id, request, ip, ct);
                return Results.Ok(customer);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { message = "Customer not found." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("AdminUpdateCustomerStatus")
        .WithSummary("Activate or Deactivate Customer Account")
        .WithDescription("Enables authorized staff to deactivate or reactivate a customer account within permitted branch scope. Deactivation requires a reason and immediately revokes all sessions/refresh tokens.")
        .Produces<AdminCustomerDetailDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
