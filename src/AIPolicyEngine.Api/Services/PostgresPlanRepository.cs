using AIPolicyEngine.Api.Models;

namespace AIPolicyEngine.Api.Services;

/// <summary>
/// Postgres-backed persistence for billing plans.
/// Partition key: "plan", document id: plan ID.
/// </summary>
public sealed class PostgresPlanRepository : PostgresRepositoryBase<PlanData>
{
    public PostgresPlanRepository(PostgresDocumentStore provider, ILogger<PostgresPlanRepository> logger)
        : base(provider, "plan", logger) { }

    protected override void PrepareEntity(PlanData entity)
        => entity.PartitionKey = "plan";
}
