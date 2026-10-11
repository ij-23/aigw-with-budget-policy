using System.Text.Json;
using AIPolicyEngine.Api.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace AIPolicyEngine.Api.Services;

// JSONB preserves the existing API contracts. Primary keys and transactions are relational.
public sealed class PostgresDocumentStore(NpgsqlDataSource source)
{
    private readonly SemaphoreSlim initLock = new(1, 1);
    private bool initialized;
    public NpgsqlDataSource Source => source;

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (initialized) return;
        await initLock.WaitAsync(ct);
        try
        {
            if (initialized) return;
            await using var connection = await source.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            // Coordinate schema setup across replicas. Versioned SQL is shipped with the application.
            await using var guard = new NpgsqlCommand("SELECT pg_advisory_xact_lock(727631003)", connection, transaction);
            await guard.ExecuteNonQueryAsync(ct);
            var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Database", "001_initial.sql"), ct);
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
            initialized = true;
        }
        finally { initLock.Release(); }
    }

    public async Task<T?> GetAsync<T>(string scope, string id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await using var command = source.CreateCommand("SELECT body::text FROM configuration_documents WHERE scope=$1 AND id=$2");
        command.Parameters.AddWithValue(scope); command.Parameters.AddWithValue(id);
        var json = await command.ExecuteScalarAsync(ct) as string;
        return json is null ? default : JsonSerializer.Deserialize<T>(json, JsonConfig.Default);
    }

    public async Task<List<T>> ListAsync<T>(string scope, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await using var command = source.CreateCommand("SELECT body::text FROM configuration_documents WHERE scope=$1 ORDER BY id");
        command.Parameters.AddWithValue(scope);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<T>();
        while (await reader.ReadAsync(ct)) result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), JsonConfig.Default)!);
        return result;
    }

    public async Task<T> UpsertAsync<T>(string scope, string id, T entity, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await using var command = source.CreateCommand("INSERT INTO configuration_documents(scope,id,body) VALUES($1,$2,$3::jsonb) ON CONFLICT(scope,id) DO UPDATE SET body=EXCLUDED.body");
        command.Parameters.AddWithValue(scope); command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(entity, JsonConfig.Default));
        await command.ExecuteNonQueryAsync(ct);
        return entity;
    }

    public async Task<bool> DeleteAsync(string scope, string id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        await using var command = source.CreateCommand("DELETE FROM configuration_documents WHERE scope=$1 AND id=$2");
        command.Parameters.AddWithValue(scope); command.Parameters.AddWithValue(id);
        return await command.ExecuteNonQueryAsync(ct) != 0;
    }
}

public sealed class PostgresHealthCheck(PostgresDocumentStore store) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await store.EnsureInitializedAsync(cancellationToken);
            await using var command = store.Source.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.", ex); }
    }
}
