using AIPolicyEngine.Api.Models;
using AIPolicyEngine.Api.Services.Budgets;
using Microsoft.Azure.Cosmos;

namespace AIPolicyEngine.Api.Endpoints;

public static class BudgetEndpoints
{
    public static IEndpointRouteBuilder MapBudgetEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/budgets/{tenantId}");
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (!Guid.TryParse(http.Request.RouteValues["tenantId"]?.ToString(), out _))
                return Results.BadRequest(new { error = "Tenant ID must be a UUID." });
            try { return await next(context); }
            catch (BudgetException ex)
            {
                http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BudgetEnforcement")
                    .LogWarning("Budget operation rejected: {Code}, tenant {TenantId}, trace {TraceId}", ex.Code, http.Request.RouteValues["tenantId"], http.TraceIdentifier);
                if (ex.ResetsAt is { } reset)
                    http.Response.Headers.RetryAfter = Math.Max(1, (long)(reset - DateTimeOffset.UtcNow).TotalSeconds).ToString();
                return Results.Json(new { error = ex.Message, code = ex.Code, resetsAt = ex.ResetsAt }, statusCode: ex.Status);
            }
            catch (CosmosException)
            {
                return Results.Json(new { error = "Budget ledger is unavailable. No new request is authorized.", code = "budget_store_unavailable" }, statusCode: 503);
            }
        });
        group.MapGet("/configuration", (string tenantId, BudgetService service, CancellationToken ct) =>
            service.GetConfigurationAsync(Normalize(tenantId), ct)).RequireAuthorization("AdminPolicy");
        group.MapPut("/configuration", (string tenantId, BudgetConfiguration config, BudgetService service, CancellationToken ct) =>
            service.SaveConfigurationAsync(Normalize(tenantId), config, ct)).RequireAuthorization("AdminPolicy");
        group.MapGet("/balances", (string tenantId, IBudgetStore store, CancellationToken ct) =>
            store.ListAsync<BudgetBalance>(Normalize(tenantId), "balance", ct)).RequireAuthorization("AdminPolicy");
        group.MapGet("/reservations", (string tenantId, IBudgetStore store, CancellationToken ct) =>
            store.ListAsync<BudgetReservation>(Normalize(tenantId), "reservation", ct)).RequireAuthorization("AdminPolicy");
        // Only the gateway service role may assert the original caller's validated identity.
        group.MapPost("/reserve", (string tenantId, BudgetReserveRequest request, BudgetService service, CancellationToken ct) =>
            service.ReserveAsync(Normalize(tenantId), request, ct)).RequireAuthorization("ApimPolicy");
        group.MapPost("/reservations/{requestId}/settle", (string tenantId, string requestId,
            BudgetSettleRequest request, BudgetService service, CancellationToken ct) =>
            service.SettleAsync(Normalize(tenantId), requestId, request with { Note = null }, ct)).RequireAuthorization("ApimPolicy");
        group.MapPost("/reservations/{requestId}/reconcile", (string tenantId, string requestId,
            BudgetSettleRequest request, BudgetService service, HttpContext http, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 2000)
                throw new BudgetException(400, "evidence_required", "Provide a reconciliation note describing the verified backend usage.");
            var actor = http.User.FindFirst("oid")?.Value ?? http.User.Identity?.Name ?? "admin";
            return service.SettleAsync(Normalize(tenantId), requestId, request with { Note = $"Reconciled by {actor}: {request.Note}" }, ct);
        }).RequireAuthorization("AdminPolicy");
        return routes;
    }

    private static string Normalize(string value) => Guid.Parse(value).ToString("D");
}
