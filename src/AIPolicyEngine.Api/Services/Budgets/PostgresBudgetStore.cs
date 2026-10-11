using System.Data;
using System.Text.Json;
using AIPolicyEngine.Api.Models;
using Npgsql;

namespace AIPolicyEngine.Api.Services.Budgets;

// Every financial mutation takes the same transaction-scoped tenant lock before reading.
// ReadCommitted obtains fresh snapshots AFTER the lock. A waiting caller sees the previous
// transaction's commit. Locks are released automatically on commit/rollback/connection loss.
public sealed class PostgresBudgetStore(PostgresDocumentStore provider) : IBudgetStore
{
    public async Task<T> TransactAsync<T>(string tenantId, Func<IBudgetTransaction, Task<T>> action, CancellationToken ct = default)
    {
        await provider.EnsureInitializedAsync(ct);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                await using var connection = await provider.Source.OpenConnectionAsync(ct);
                await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
                await using var timeout = new NpgsqlCommand("SET LOCAL lock_timeout = '5s'", connection, transaction);
                await timeout.ExecuteNonQueryAsync(ct);
                await using var guard = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1, 0))", connection, transaction);
                guard.Parameters.AddWithValue("budget:" + tenantId);
                await guard.ExecuteNonQueryAsync(ct);
                var ledger = new Transaction(connection, transaction, tenantId, ct);
                var result = await action(ledger);
                await ledger.FlushAsync();
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (PostgresException ex) when (ex.SqlState is "55P03" or "40P01" or "40001")
            {
                if (attempt == 7) throw new BudgetException(503, "budget_busy", "Budget ledger is busy. Retry the same request ID.");
                await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), ct);
            }
            catch (NpgsqlException)
            {
                // An uncertain commit must never return an authorization. A repeat reservation
                // cannot authorize another inference, even if the earlier commit succeeded.
                throw new BudgetException(503, "budget_store_unavailable", "Budget ledger is unavailable. No new request is authorized.");
            }
        }
        throw new BudgetException(503, "budget_busy", "Budget ledger is busy.");
    }

    public async Task<IReadOnlyList<T>> ListAsync<T>(string tenantId, string kind, CancellationToken ct = default) where T : BudgetDocument
    {
        await provider.EnsureInitializedAsync(ct);
        await using var command = provider.Source.CreateCommand("SELECT body::text FROM budget_documents WHERE tenant_id=$1 AND kind=$2 ORDER BY id LIMIT 500");
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(kind);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<T>();
        while (await reader.ReadAsync(ct)) result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), JsonConfig.Default)!);
        return result;
    }

    internal sealed class Transaction(NpgsqlConnection connection, NpgsqlTransaction transaction, string tenantId, CancellationToken ct) : IBudgetTransaction
    {
        private readonly Dictionary<string, BudgetDocument?> reads = [];
        private readonly Dictionary<string, BudgetDocument> writes = [];
        public async Task<T?> ReadAsync<T>(string id) where T : BudgetDocument
        {
            if (writes.TryGetValue(id, out var pending)) return (T)pending;
            if (reads.TryGetValue(id, out var existing)) return (T?)existing;
            await using var command = new NpgsqlCommand("SELECT body::text FROM budget_documents WHERE tenant_id=$1 AND id=$2", connection, transaction);
            command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(id);
            var json = await command.ExecuteScalarAsync(ct) as string;
            var result = json is null ? null : JsonSerializer.Deserialize<T>(json, JsonConfig.Default);
            reads[id] = result;
            return result;
        }
        public void Write(BudgetDocument document)
        {
            if (!reads.ContainsKey(document.Id)) throw new InvalidOperationException("Read before writing a budget document.");
            document.PartitionKey = "budget:" + tenantId;
            writes[document.Id] = document;
        }
        public async Task FlushAsync()
        {
            foreach (var doc in writes.Values)
            {
                await using var command = new NpgsqlCommand("INSERT INTO budget_documents(tenant_id,id,kind,body) VALUES($1,$2,$3,$4::jsonb) ON CONFLICT(tenant_id,id) DO UPDATE SET kind=EXCLUDED.kind,body=EXCLUDED.body", connection, transaction);
                command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(doc.Id); command.Parameters.AddWithValue(doc.Kind);
                command.Parameters.AddWithValue(JsonSerializer.Serialize(doc, doc.GetType(), JsonConfig.Default));
                await command.ExecuteNonQueryAsync(ct);
            }
        }
    }
}
