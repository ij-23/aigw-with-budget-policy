using AIPolicyEngine.Api.Models;

namespace AIPolicyEngine.Api.Services;

/// <summary>
/// Postgres-backed persistence for the singleton usage policy settings document.
/// Partition key: "settings", document id: "usage-policy".
/// </summary>
public sealed class PostgresUsagePolicyRepository : PostgresRepositoryBase<UsagePolicySettings>
{
    public PostgresUsagePolicyRepository(PostgresDocumentStore provider, ILogger<PostgresUsagePolicyRepository> logger)
        : base(provider, "settings", logger) { }

    protected override void PrepareEntity(UsagePolicySettings entity)
    {
        entity.Id = "usage-policy";
        entity.PartitionKey = "settings";
    }
}
