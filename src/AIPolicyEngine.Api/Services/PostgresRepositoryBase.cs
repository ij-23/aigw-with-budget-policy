using System.Text.Json;
using AIPolicyEngine.Api.Models;
namespace AIPolicyEngine.Api.Services;

public abstract class PostgresRepositoryBase<T>(PostgresDocumentStore provider, string scope, ILogger logger)
    : IRepository<T> where T : class
{
    protected readonly ILogger Logger = logger;
    protected abstract void PrepareEntity(T entity);
    public Task<T?> GetAsync(string id, CancellationToken ct = default) => provider.GetAsync<T>(scope, id, ct);
    public Task<List<T>> GetAllAsync(CancellationToken ct = default) => provider.ListAsync<T>(scope, ct);
    public Task<bool> DeleteAsync(string id, CancellationToken ct = default) => provider.DeleteAsync(scope, id, ct);
    public Task<T> UpsertAsync(T entity, CancellationToken ct = default)
    {
        PrepareEntity(entity);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(entity, JsonConfig.Default));
        var id = json.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("Entity ID required.");
        return provider.UpsertAsync(scope, id, entity, ct);
    }
}
