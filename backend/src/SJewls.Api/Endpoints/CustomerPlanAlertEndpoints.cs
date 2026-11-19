using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;

namespace SJewls.Api.Endpoints;

public static class CustomerPlanAlertEndpoints
{
    public static void MapCustomerPlanAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customer")
            .WithTags("Customer Plan Alerts")
            .RequireAuthorization();

        group.MapPost("/plan-alerts/{alertId:guid}/acknowledge", async (
            Guid alertId,
            ClaimsPrincipal user,
            IPlanAlertService service,
            CancellationToken ct) =>
        {
            if (!TryGetCustomerId(user, out var customerId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var acknowledgedDto = await service.AcknowledgeAlertAsync(customerId, alertId, ct);
                return Results.Ok(new AcknowledgePlanAlertResponse
                {
                    Success = true,
                    Message = "Plan alert acknowledged successfully.",
                    Alert = acknowledgedDto
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .WithName("CustomerAcknowledgePlanAlert")
        .WithSummary("Acknowledge plan popup alert")
        .WithDescription("Records customer acknowledgement and dismissal of an investment popup alert (Chitu raffle winner or Jewellery plan completion). Subsequent investment queries will mark shouldShowPopup=false while retaining the alert for permanent banner display. Safe and idempotent on repeat calls.")
        .Produces<AcknowledgePlanAlertResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static bool TryGetCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        customerId = Guid.Empty;
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(sub, out customerId);
    }
}
