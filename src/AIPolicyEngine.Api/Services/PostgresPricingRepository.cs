using AIPolicyEngine.Api.Models;

namespace AIPolicyEngine.Api.Services;

/// <summary>
/// Postgres-backed persistence for model pricing configuration.
/// Partition key: "pricing", document id: model ID.
/// </summary>
public sealed class PostgresPricingRepository : PostgresRepositoryBase<ModelPricing>
{
    public PostgresPricingRepository(PostgresDocumentStore provider, ILogger<PostgresPricingRepository> logger)
        : base(provider, "pricing", logger) { }

    protected override void PrepareEntity(ModelPricing entity)
    {
        entity.Id = entity.ModelId;
        entity.PartitionKey = "pricing";
    }
}
