using AIPolicyEngine.Api.Models;
namespace AIPolicyEngine.Api.Services.AccessProfiles;
public sealed class PostgresAccessProfileRepository(PostgresDocumentStore provider, ILogger<PostgresAccessProfileRepository> logger) : IAccessProfileRepository
{
    public Task<AccessProfile?> GetAsync(string profileId, CancellationToken ct = default) => provider.GetAsync<AccessProfile>(AccessProfile.PartitionKeyValue, profileId, ct);
    public Task<AccessProfile?> GetForScopeAsync(string clientAppId, string tenantId, string apiId, string? operationId, CancellationToken ct = default)
        => GetAsync(AccessProfile.BuildId(clientAppId, tenantId, apiId, operationId), ct);
    public async Task<List<AccessProfile>> ListAsync(string? clientAppId = null, string? tenantId = null, string? apiId = null, CancellationToken ct = default)
        => (await provider.ListAsync<AccessProfile>(AccessProfile.PartitionKeyValue, ct))
            .Where(p => string.IsNullOrWhiteSpace(clientAppId) || p.ClientAppId == clientAppId.Trim())
            .Where(p => string.IsNullOrWhiteSpace(tenantId) || p.TenantId == tenantId.Trim())
            .Where(p => string.IsNullOrWhiteSpace(apiId) || p.ApiId == apiId.Trim())
            .OrderBy(p => p.ClientAppId).ThenBy(p => p.TenantId).ThenBy(p => p.ApiId).ThenBy(p => p.OperationId).ToList();
    public Task<AccessProfile> UpsertAsync(AccessProfile profile, CancellationToken ct = default)
    {
        PrepareEntity(profile);
        logger.LogInformation("Access profile upserted: {ProfileId}", profile.Id);
        return provider.UpsertAsync(AccessProfile.PartitionKeyValue, profile.Id, profile, ct);
    }
    public Task<bool> DeleteAsync(string profileId, CancellationToken ct = default) => provider.DeleteAsync(AccessProfile.PartitionKeyValue, profileId, ct);
    private static void PrepareEntity(AccessProfile profile)
    {
        profile.ClientAppId = profile.ClientAppId.Trim();
        profile.TenantId = profile.TenantId.Trim();
        profile.ApiId = profile.ApiId.Trim();
        profile.OperationId = string.IsNullOrWhiteSpace(profile.OperationId) ? null : profile.OperationId.Trim();
        profile.PlanId = profile.PlanId.Trim();
        profile.RoutingPolicyId = string.IsNullOrWhiteSpace(profile.RoutingPolicyId) ? null : profile.RoutingPolicyId.Trim();
        profile.AllowedDeployments = (profile.AllowedDeployments ?? [])
            .Where(static deployment => !string.IsNullOrWhiteSpace(deployment))
            .Select(static deployment => deployment.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        profile.Id = AccessProfile.BuildId(profile.ClientAppId, profile.TenantId, profile.ApiId, profile.OperationId);
        profile.PartitionKey = AccessProfile.PartitionKeyValue;
    }
}
