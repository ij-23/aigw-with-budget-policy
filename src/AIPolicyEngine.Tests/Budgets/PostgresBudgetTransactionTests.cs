using AIPolicyEngine.Api.Models;
using AIPolicyEngine.Api.Services;
using AIPolicyEngine.Api.Services.Budgets;
using Npgsql;
namespace AIPolicyEngine.Tests.Budgets;

public sealed class PostgresBudgetTransactionTests
{
    private static NpgsqlDataSource Source() => NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")!);

    [PostgresFact]
    public async Task ConcurrentReservations_DoNotOverspend_AndPreserveDecimalFields()
    {
        await using var source = Source();
        var store = new PostgresBudgetStore(new PostgresDocumentStore(source));
        var tenant = Guid.NewGuid().ToString();
        var accepted = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => store.TransactAsync(tenant, async tx =>
        {
            var balance = await tx.ReadAsync<BudgetBalance>("balance") ?? new BudgetBalance { Id = "balance", Kind = "balance", LimitUsd = 50m };
            if (balance.ReservedUsd + 3.123456789m > balance.LimitUsd) return false;
            await Task.Delay(2);
            balance.ReservedUsd += 3.123456789m;
            tx.Write(balance);
            return true;
        })));
        Assert.Equal(16, accepted.Count(x => x));
        var saved = Assert.Single(await store.ListAsync<BudgetBalance>(tenant, "balance"));
        Assert.Equal(49.975308624m, saved.ReservedUsd);
        Assert.Equal(50m, saved.LimitUsd);
        Assert.Equal("budget:" + tenant, saved.PartitionKey);
    }

    [PostgresFact]
    public async Task CallbackFailure_RollsBackAllWrites()
    {
        await using var source = Source();
        var store = new PostgresBudgetStore(new PostgresDocumentStore(source));
        var tenant = Guid.NewGuid().ToString();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.TransactAsync<int>(tenant, async tx =>
        {
            await tx.ReadAsync<BudgetBalance>("first");
            tx.Write(new BudgetBalance { Id = "first", Kind = "balance", ReservedUsd = 20 });
            throw new InvalidOperationException("abort");
        }));
        Assert.Empty(await store.ListAsync<BudgetBalance>(tenant, "balance"));
    }

    [PostgresFact]
    public async Task ConfigurationAndAllMatchedBalances_CommitTogether_AndTenantsAreIsolated()
    {
        await using var source = Source();
        var store = new PostgresBudgetStore(new PostgresDocumentStore(source));
        var tenant = Guid.NewGuid().ToString();
        await store.TransactAsync(tenant, async tx =>
        {
            await tx.ReadAsync<BudgetConfiguration>("config");
            tx.Write(new BudgetConfiguration { Id = "config", Kind = "configuration", Revision = 42 });
            foreach (var id in new[] { "user", "group" })
            {
                await tx.ReadAsync<BudgetBalance>(id);
                tx.Write(new BudgetBalance { Id = id, Kind = "balance", ReservedUsd = 1.23456789m });
            }
            return true;
        });
        Assert.Equal(42, Assert.Single(await store.ListAsync<BudgetConfiguration>(tenant, "configuration")).Revision);
        Assert.Equal(2, (await store.ListAsync<BudgetBalance>(tenant, "balance")).Count);
        Assert.Empty(await store.ListAsync<BudgetBalance>(Guid.NewGuid().ToString(), "balance"));
    }

    [PostgresFact]
    public async Task RealBudgetService_ReservesSettlesIdempotentlyAndBlocksAtLimit()
    {
        await using var source = Source();
        var store = new PostgresBudgetStore(new PostgresDocumentStore(source));
        var service = new BudgetService(store, TimeProvider.System);
        var tenant = Guid.NewGuid().ToString();
        await service.SaveConfigurationAsync(tenant, BudgetServiceTests.Configuration(2));
        var request = BudgetServiceTests.Request();
        await service.ReserveAsync(tenant, request);
        var blocked = await Assert.ThrowsAsync<BudgetException>(() => service.ReserveAsync(tenant, BudgetServiceTests.Request()));
        Assert.Equal("budget_exceeded", blocked.Code);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => service.SettleAsync(tenant, request.RequestId, new BudgetSettleRequest(new(100, 100)))));
        var balance = Assert.Single(await store.ListAsync<BudgetBalance>(tenant, "balance"));
        Assert.Equal(0.2m, balance.SpentUsd);
        Assert.Equal(0m, balance.ReservedUsd);
        Assert.Equal(1.8m, balance.RemainingUsd);
        var duplicate = await Assert.ThrowsAsync<BudgetException>(() => service.ReserveAsync(tenant, request));
        Assert.Equal(409, duplicate.Status);
    }

    [Fact]
    public async Task UnavailableDatabase_CannotAuthorizeARequest()
    {
        await using var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=test;Database=test;Timeout=1");
        var store = new PostgresBudgetStore(new PostgresDocumentStore(source));
        await Assert.ThrowsAnyAsync<NpgsqlException>(() => store.TransactAsync("tenant", _ => Task.FromResult(true)));
    }
}
