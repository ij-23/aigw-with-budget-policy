using AIPolicyEngine.Api.Models;

namespace AIPolicyEngine.Api.Services.Budgets;

public interface IBudgetTransaction
{
    Task<T?> ReadAsync<T>(string id) where T : BudgetDocument;
    void Write(BudgetDocument document);
}

public interface IBudgetStore
{
    // Callback can run again after contention. No external side effects inside it.
    Task<T> TransactAsync<T>(string tenantId, Func<IBudgetTransaction, Task<T>> action,
        CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync<T>(string tenantId, string kind, CancellationToken ct = default)
        where T : BudgetDocument;
}
