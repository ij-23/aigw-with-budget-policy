using AIPolicyEngine.Api.Models;
using AIPolicyEngine.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
namespace AIPolicyEngine.Tests.Integration;
public sealed class PostgresAuditStoreTests
{
    [PostgresFact]
    public async Task SummaryRetries_AreIdempotent_AndConcurrentUpdatesAreNotLost()
    {
        await using var source = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")!);
        var store = new AuditStore(new PostgresDocumentStore(source), NullLogger<AuditStore>.Instance);
        var client = Guid.NewGuid().ToString();
        var items = Enumerable.Range(0, 15).Select(i => new AuditLogItem { ClientAppId = client, TenantId = "tenant", DeploymentId = "model", CorrelationId = i.ToString(), Timestamp = new DateTime(2026,10,11,0,0,0,DateTimeKind.Utc), PromptTokens = 5, TotalTokens = 5, CostToUs = "0.0001" }).ToArray();
        await Task.WhenAll(items.Select(item => store.UpsertBillingSummariesAsync([item])));
        await store.UpsertBillingSummariesAsync(items);
        var summary = Assert.Single((await store.GetBillingSummariesAsync("2026-10")).Where(s => s.ClientAppId == client));
        Assert.Equal(15, summary.RequestCount);
        Assert.Equal(75, summary.TotalTokens);
        Assert.Equal(0.0015m, summary.CostToUs);
    }
}
