using System.Globalization;
using System.Text.Json;
using AIPolicyEngine.Api.Models;
using Npgsql;
namespace AIPolicyEngine.Api.Services;

public sealed class AuditStore(PostgresDocumentStore provider, ILogger<AuditStore> logger) : IAuditStore
{
    public async Task WriteBatchAsync(IReadOnlyList<AuditLogDocument> documents, CancellationToken ct = default)
    {
        await provider.EnsureInitializedAsync(ct);
        await using var connection = await provider.Source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        foreach (var doc in documents)
            await UpsertAsync(connection, transaction, "audit_documents", doc.CustomerKey, doc.Id, doc, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task UpsertBillingSummariesAsync(IReadOnlyList<AuditLogItem> items, CancellationToken ct = default)
    {
        await provider.EnsureInitializedAsync(ct);
        foreach (var group in items.GroupBy(i => (i.ClientAppId, i.TenantId, i.DeploymentId, Period: $"{i.Timestamp:yyyy-MM}")))
        {
            var (clientAppId, tenantId, deploymentId, period) = group.Key;
            var customerKey = $"{clientAppId}:{tenantId}";
            var id = $"{clientAppId}:{tenantId}:{deploymentId}:{period}";
            await using var connection = await provider.Source.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await using var guard = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1, 0))", connection, transaction);
            guard.Parameters.AddWithValue("billing:" + id);
            await guard.ExecuteNonQueryAsync(ct);
            await using var get = new NpgsqlCommand("SELECT body::text FROM billing_summaries WHERE customer_key=$1 AND id=$2", connection, transaction);
            get.Parameters.AddWithValue(customerKey); get.Parameters.AddWithValue(id);
            var json = await get.ExecuteScalarAsync(ct) as string;
            var first = group.First();
            var summary = json is null ? new BillingSummaryDocument
            {
                Id = id, CustomerKey = customerKey, ClientAppId = clientAppId, TenantId = tenantId,
                DeploymentId = deploymentId, BillingPeriod = period, DisplayName = first.DisplayName,
                Audience = first.Audience, Model = first.Model
            } : JsonSerializer.Deserialize<BillingSummaryDocument>(json, JsonConfig.Default)!;
            foreach (var item in group)
            {
                // Use the same deterministic identifier as the audit writer for retry deduplication.
                var requestId = AuditLogWriter.GenerateDeterministicId(item);
                await using var dedup = new NpgsqlCommand("INSERT INTO billing_processed_requests(customer_key,request_id) VALUES($1,$2) ON CONFLICT DO NOTHING", connection, transaction);
                dedup.Parameters.AddWithValue(customerKey); dedup.Parameters.AddWithValue(requestId);
                if (await dedup.ExecuteNonQueryAsync(ct) == 0) continue;
                summary.PromptTokens += item.PromptTokens; summary.CompletionTokens += item.CompletionTokens;
                summary.TotalTokens += item.TotalTokens; summary.ImageTokens += item.ImageTokens; summary.RequestCount++;
                if (decimal.TryParse(item.CostToUs, NumberStyles.Number, CultureInfo.InvariantCulture, out var cost)) summary.CostToUs += cost;
                if (decimal.TryParse(item.CostToCustomer, NumberStyles.Number, CultureInfo.InvariantCulture, out var customerCost)) summary.CostToCustomer += customerCost;
                summary.IsOverbilled |= item.IsOverbilled;
                if (item.EffectiveRequestCost.HasValue)
                {
                    summary.TotalEffectiveRequests = (summary.TotalEffectiveRequests ?? 0m) + item.EffectiveRequestCost.Value;
                    if (!string.IsNullOrEmpty(item.TierName))
                    {
                        summary.EffectiveRequestsByTier ??= new();
                        summary.EffectiveRequestsByTier[item.TierName] = summary.EffectiveRequestsByTier.GetValueOrDefault(item.TierName) + item.EffectiveRequestCost.Value;
                    }
                }
                if (item.MultiplierOverageCost is > 0)
                    summary.MultiplierOverageCost = (summary.MultiplierOverageCost ?? 0m) + item.MultiplierOverageCost.Value;
                if (!string.IsNullOrEmpty(item.DisplayName)) summary.DisplayName = item.DisplayName;
            }
            summary.UpdatedAt = DateTime.UtcNow;
            await UpsertAsync(connection, transaction, "billing_summaries", customerKey, id, summary, ct);
            await transaction.CommitAsync(ct);
        }
        logger.LogDebug("Billing summaries committed to PostgreSQL.");
    }

    public Task<List<BillingSummaryDocument>> GetBillingSummariesAsync(string billingPeriod, CancellationToken ct = default)
        => QueryAsync<BillingSummaryDocument>("SELECT body::text FROM billing_summaries WHERE body->>'billingPeriod'=$1 ORDER BY id", [billingPeriod], ct);
    public Task<List<AuditLogDocument>> GetClientAuditLogsAsync(string customerKey, string billingPeriod, CancellationToken ct = default)
        => QueryAsync<AuditLogDocument>("SELECT body::text FROM audit_documents WHERE customer_key=$1 AND body->>'billingPeriod'=$2 ORDER BY body->>'timestamp' DESC", [customerKey, billingPeriod], ct);
    public async Task<List<ExportPeriod>> GetAvailablePeriodsAsync(CancellationToken ct = default)
    {
        var summaries = await QueryAsync<BillingSummaryDocument>("SELECT body::text FROM billing_summaries", [], ct);
        return summaries.Select(s => s.BillingPeriod).Distinct()
            .Where(p => DateTime.TryParseExact(p, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .OrderDescending().Select(p => new ExportPeriod { Year = int.Parse(p[..4]), Month = int.Parse(p[5..]) }).ToList();
    }
    public async Task<List<ExportClient>> GetClientsForPeriodAsync(string billingPeriod, CancellationToken ct = default)
        => (await GetBillingSummariesAsync(billingPeriod, ct)).GroupBy(r => $"{r.ClientAppId}:{r.TenantId}", StringComparer.OrdinalIgnoreCase)
            .Select(g => new ExportClient { ClientAppId = g.First().ClientAppId, TenantId = g.First().TenantId, DisplayName = g.First().DisplayName })
            .OrderBy(c => c.DisplayName).ToList();

    private async Task<List<T>> QueryAsync<T>(string sql, string[] parameters, CancellationToken ct)
    {
        await provider.EnsureInitializedAsync(ct);
        await using var command = provider.Source.CreateCommand(sql);
        foreach (var value in parameters) command.Parameters.AddWithValue(value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var results = new List<T>();
        while (await reader.ReadAsync(ct)) results.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), JsonConfig.Default)!);
        return results;
    }
    private static async Task UpsertAsync<T>(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, string customerKey, string id, T doc, CancellationToken ct)
    {
        // Table name is an internal constant, never request data.
        await using var command = new NpgsqlCommand($"INSERT INTO {table}(customer_key,id,body) VALUES($1,$2,$3::jsonb) ON CONFLICT(customer_key,id) DO UPDATE SET body=EXCLUDED.body", connection, transaction);
        command.Parameters.AddWithValue(customerKey); command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(doc, JsonConfig.Default));
        await command.ExecuteNonQueryAsync(ct);
    }
}
