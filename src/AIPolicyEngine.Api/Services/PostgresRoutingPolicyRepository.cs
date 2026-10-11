using AIPolicyEngine.Api.Models;

namespace AIPolicyEngine.Api.Services;

/// <summary>
/// Postgres-backed persistence for model routing policies.
/// Partition key: "routing-policy", document id: policy ID.
/// </summary>
public sealed class PostgresRoutingPolicyRepository : PostgresRepositoryBase<ModelRoutingPolicy>
{
    public PostgresRoutingPolicyRepository(PostgresDocumentStore provider, ILogger<PostgresRoutingPolicyRepository> logger)
        : base(provider, "routing-policy", logger) { }

    protected override void PrepareEntity(ModelRoutingPolicy entity)
        => entity.PartitionKey = "routing-policy";
}
