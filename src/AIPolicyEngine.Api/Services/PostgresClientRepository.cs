using AIPolicyEngine.Api.Models;

namespace AIPolicyEngine.Api.Services;

/// <summary>
/// Postgres-backed persistence for client plan assignments.
/// Partition key: "client", document id: "{clientAppId}:{tenantId}".
/// </summary>
public sealed class PostgresClientRepository : PostgresRepositoryBase<ClientPlanAssignment>
{
    public PostgresClientRepository(PostgresDocumentStore provider, ILogger<PostgresClientRepository> logger)
        : base(provider, "client", logger) { }

    protected override void PrepareEntity(ClientPlanAssignment entity)
    {
        entity.Id = $"{entity.ClientAppId}:{entity.TenantId}";
        entity.PartitionKey = "client";
    }
}
