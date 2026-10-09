using System.Net;
using AIPolicyEngine.Api.Models;
using Microsoft.Azure.Cosmos;

namespace AIPolicyEngine.Api.Services.Budgets;

// A tenant's configuration, balances and reservations share one logical partition.
// ETag-guarded transactional batches atomically validate every read and write all debits.
// No monetary authority or locks live in Redis. Never configure multiple write regions
// for this ledger: conflict resolution is not a substitute for a serial budget authority.
public sealed class CosmosBudgetStore(ConfigurationContainerProvider provider) : IBudgetStore
{
    public async Task<T> TransactAsync<T>(string tenantId, Func<IBudgetTransaction, Task<T>> action,
        CancellationToken ct = default)
    {
        await provider.EnsureInitializedAsync(ct);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var transaction = new Transaction(provider.Container, "budget:" + tenantId, ct);
            var result = await action(transaction);
            if (await transaction.CommitAsync()) return result;
            await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), ct);
        }
        throw new BudgetException(503, "budget_busy", "Budget ledger is busy. Retry the same request ID.");
    }

    public async Task<IReadOnlyList<T>> ListAsync<T>(string tenantId, string kind, CancellationToken ct = default)
        where T : BudgetDocument
    {
        await provider.EnsureInitializedAsync(ct);
        using var iterator = provider.Container.GetItemQueryIterator<T>(
            new QueryDefinition("SELECT TOP 500 * FROM c WHERE c.kind = @kind ORDER BY c.id")
                .WithParameter("@kind", kind),
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey("budget:" + tenantId) });
        var result = new List<T>();
        while (iterator.HasMoreResults) result.AddRange(await iterator.ReadNextAsync(ct));
        return result;
    }

    internal sealed class Transaction(Container container, string partition, CancellationToken ct) : IBudgetTransaction
    {
        private readonly Dictionary<string, (BudgetDocument? Document, string? ETag)> reads = [];
        private readonly Dictionary<string, BudgetDocument> writes = [];

        public async Task<T?> ReadAsync<T>(string id) where T : BudgetDocument
        {
            if (reads.TryGetValue(id, out var existing)) return (T?)existing.Document;
            try
            {
                var response = await container.ReadItemAsync<T>(id, new PartitionKey(partition), cancellationToken: ct);
                reads.Add(id, (response.Resource, response.ETag));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                reads.Add(id, (null, null));
                return null;
            }
        }

        public void Write(BudgetDocument document)
        {
            if (!reads.ContainsKey(document.Id)) throw new InvalidOperationException("Read before writing a budget document.");
            document.PartitionKey = partition;
            writes[document.Id] = document;
        }

        public async Task<bool> CommitAsync()
        {
            if (writes.Count == 0) return true;
            var batch = container.CreateTransactionalBatch(new PartitionKey(partition));
            // Replacing unchanged read documents guards policy edits and concurrent settlements.
            foreach (var (id, read) in reads)
            {
                var document = writes.GetValueOrDefault(id) ?? read.Document;
                if (document is null) continue;
                // Serialize the runtime type: passing BudgetDocument would drop derived fields.
                using var json = System.Text.Json.JsonDocument.Parse(
                    System.Text.Json.JsonSerializer.Serialize(document, document.GetType(), JsonConfig.Default));
                var body = json.RootElement.Clone();
                if (read.ETag is null) batch.CreateItem(body);
                else batch.ReplaceItem(id, body, new TransactionalBatchItemRequestOptions { IfMatchEtag = read.ETag });
            }
            using var response = await batch.ExecuteAsync(ct);
            if (response.IsSuccessStatusCode) return true;
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed) return false;
            throw new BudgetException(503, "budget_store_unavailable", "Budget ledger could not commit the operation.");
        }
    }
}
