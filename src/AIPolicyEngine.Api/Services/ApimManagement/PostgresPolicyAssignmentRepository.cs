using AIPolicyEngine.Api.Models.Apim;
using AIPolicyEngine.Api.Services;

namespace AIPolicyEngine.Api.Services.ApimManagement;

public sealed class PostgresPolicyAssignmentRepository : PostgresRepositoryBase<PolicyAssignment>, IPolicyAssignmentRepository
{
    public PostgresPolicyAssignmentRepository(PostgresDocumentStore provider, ILogger<PostgresPolicyAssignmentRepository> logger)
        : base(provider, "policy-assignment", logger)
    {
    }

    public Task<PolicyAssignment?> GetAsync(string apiId, string? operationId, CancellationToken ct = default)
        => base.GetAsync(PolicyAssignment.BuildId(apiId, operationId), ct);

    public Task<bool> DeleteAsync(string apiId, string? operationId, CancellationToken ct = default)
        => base.DeleteAsync(PolicyAssignment.BuildId(apiId, operationId), ct);

    protected override void PrepareEntity(PolicyAssignment entity)
    {
        entity.Id = PolicyAssignment.BuildId(entity.ApiId, entity.OperationId);
        entity.PartitionKey = "policy-assignment";
    }
}
