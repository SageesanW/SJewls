using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class JewelleryCategoryEndpoints
{
    public static void MapJewelleryCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/jewellery-categories")
            .WithTags("Admin Jewellery Categories");

        // 1. GET /api/v1/admin/jewellery-categories
        group.MapGet("/", async (
            ClaimsPrincipal user,
            IJewelleryCategoryService service,
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromQuery] Guid? branchId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var result = await service.GetCategoriesAsync(staffId.Value, search, isActive, branchId, page, pageSize, ct);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireAuthorization("StaffOnly")
        .WithName("AdminGetJewelleryCategories")
        .WithSummary("List Jewellery Plan Categories")
        .WithDescription("Retrieves a paginated list of jewellery categories. Super Admins view across branches or filter by branch; Branch Staff are isolated to their assigned branch.")
        .Produces<JewelleryCategoryPagedResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 2. POST /api/v1/admin/jewellery-categories
        group.MapPost("/", async (
            ClaimsPrincipal user,
            IJewelleryCategoryService service,
            [FromBody] CreateJewelleryCategoryRequest request,
            HttpContext httpContext,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var created = await service.CreateCategoryAsync(request, staffId.Value, ip, ct);
                return Results.Created($"/api/v1/admin/jewellery-categories/{created.Id}", created);
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
        .WithName("AdminCreateJewelleryCategory")
        .WithSummary("Create Jewellery Category")
        .WithDescription("Creates a new jewellery plan category (e.g., Gold Bars, Thali Kodi, Necklace). Prevents duplicate normalized category names within the same branch.")
        .Produces<JewelleryCategoryDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 3. GET /api/v1/admin/jewellery-categories/{id}
        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IJewelleryCategoryService service,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            try
            {
                var category = await service.GetCategoryByIdAsync(id, staffId.Value, ct);
                if (category == null)
                {
                    return Results.NotFound(new { message = $"Jewellery category with ID '{id}' was not found." });
                }
                return Results.Ok(category);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireAuthorization("StaffOnly")
        .WithName("AdminGetJewelleryCategoryById")
        .WithSummary("Get Jewellery Category by ID")
        .WithDescription("Retrieves detailed jewellery category information including associated plan counts.")
        .Produces<JewelleryCategoryDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 4. PUT /api/v1/admin/jewellery-categories/{id}
        group.MapPut("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IJewelleryCategoryService service,
            [FromBody] UpdateJewelleryCategoryRequest request,
            HttpContext httpContext,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var updated = await service.UpdateCategoryAsync(id, request, staffId.Value, ip, ct);
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
        .WithName("AdminUpdateJewelleryCategory")
        .WithSummary("Update Jewellery Category")
        .WithDescription("Updates jewellery category name, description, visual image, and active status. Validates normalized name uniqueness.")
        .Produces<JewelleryCategoryDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 5. PATCH /api/v1/admin/jewellery-categories/{id}/status
        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            ClaimsPrincipal user,
            IJewelleryCategoryService service,
            [FromBody] UpdateJewelleryCategoryStatusRequest request,
            HttpContext httpContext,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                var updated = await service.UpdateCategoryStatusAsync(id, request.IsActive, staffId.Value, ip, ct);
                return Results.Ok(updated);
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
        .RequireAuthorization("BranchAdminOrSuperAdmin")
        .WithName("AdminUpdateJewelleryCategoryStatus")
        .WithSummary("Activate or Deactivate Jewellery Category")
        .WithDescription("Toggles active operational status of a jewellery category. Deactivation hides it from new plan creation without affecting existing enrolments or payments.")
        .Produces<JewelleryCategoryDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 6. DELETE /api/v1/admin/jewellery-categories/{id}
        group.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IJewelleryCategoryService service,
            HttpContext httpContext,
            CancellationToken ct = default) =>
        {
            var staffId = GetStaffId(user);
            if (staffId == null) return Results.Unauthorized();

            var ip = httpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                await service.DeleteCategoryAsync(id, staffId.Value, ip, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
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
        .WithName("AdminDeleteJewelleryCategory")
        .WithSummary("Delete Jewellery Category")
        .WithDescription("Permanently deletes an unused jewellery category. Rejects deletion if plans are associated, directing staff to deactivate instead.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // 7. POST /api/v1/admin/jewellery-categories/upload-image
        group.MapPost("/upload-image", async (
            IFormFile file,
            ISupabaseStorageService storageService,
            CancellationToken ct = default) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "No image file provided." });
            }

            if (!storageService.ValidateImageFile(file.FileName, file.ContentType, file.Length, out var validationError))
            {
                return Results.BadRequest(new { message = validationError });
            }

            try
            {
                using var stream = file.OpenReadStream();
                var publicUrl = await storageService.UploadImageAsync(stream, file.FileName, file.ContentType, "jewellery-categories", ct);
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
        .WithName("AdminUploadJewelleryCategoryImage")
        .WithSummary("Upload Category Visual Image")
        .WithDescription("Uploads a category image (PNG, JPG, WEBP, max 2MB) directly to Supabase Storage and returns the public CDN URL.")
        .Produces<ImageUploadResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }

    private static Guid? GetStaffId(ClaimsPrincipal user)
    {
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
