using System.Text.Json;
using AIPolicyEngine.Api.Models;
using AIPolicyEngine.Api.Services;
using AIPolicyEngine.Api.Services.Budgets;

namespace AIPolicyEngine.Tests.Budgets;

public sealed class BudgetServiceTests
{
    internal const string Tenant = "11111111-1111-1111-1111-111111111111";
    internal const string Client = "22222222-2222-2222-2222-222222222222";
    internal const string User = "33333333-3333-3333-3333-333333333333";
    internal const string Group = "44444444-4444-4444-4444-444444444444";
    internal const string Policy = "55555555-5555-5555-5555-555555555555";
    private readonly MemoryBudgetStore store = new();
    private readonly BudgetClock clock = new();
    private BudgetService Service => new(store, clock);

    internal static BudgetConfiguration Configuration(decimal limit = 50, string type = "User", string mode = "PerMember") => new()
    {
        Policies = [new(Policy, "AI allowance", limit)],
        Assignments = [new(Policy, type, type == "Group" ? Group : type == "Application" ? Client : User, mode)],
        // Reserve $1 for input + $1 for output. Actual 100+100 usage costs $0.20.
        Models = [new("chat", "v1", 1000, 1000, 500, 1000, 1000)]
    };

    internal static BudgetReserveRequest Request(string? user = User, string[]? groups = null) => new(
        Guid.NewGuid().ToString(), Client, user, groups ?? [], true, "chat",
        JsonSerializer.SerializeToElement(new { messages = new[] { new { role = "user", content = "Hello" } }, max_completion_tokens = 1000 }));

    [Fact]
    public async Task ConcurrentRequests_CannotSpendTheSameRemainingFunds()
    {
        await Service.SaveConfigurationAsync(Tenant, Configuration(10));
        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(async _ =>
        {
            try { await Service.ReserveAsync(Tenant, Request()); return true; }
            catch (BudgetException ex) when (ex.Code == "budget_exceeded") { return false; }
        }));
        Assert.Equal(5, results.Count(x => x));
        var balance = Assert.Single(await store.ListAsync<BudgetBalance>(Tenant, "balance"));
        Assert.Equal(10, balance.ReservedUsd);
        Assert.Equal(0, balance.RemainingUsd);
    }

    [Fact]
    public async Task Settlement_IsIdempotent_AndReleasesOnlyUnusedFunds()
    {
        await Service.SaveConfigurationAsync(Tenant, Configuration());
        var request = Request();
        await Service.ReserveAsync(Tenant, request);
        var usage = new BudgetSettleRequest(new(100, 100));
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Service.SettleAsync(Tenant, request.RequestId, usage)));
        var balance = Assert.Single(await store.ListAsync<BudgetBalance>(Tenant, "balance"));
        Assert.Equal(0.2m, balance.SpentUsd);
        Assert.Equal(0, balance.ReservedUsd);
        Assert.Equal(49.8m, balance.RemainingUsd);
        var conflict = await Assert.ThrowsAsync<BudgetException>(() => Service.SettleAsync(Tenant, request.RequestId, new(new(200, 100))));
        Assert.Equal("settlement_conflict", conflict.Code);
    }

    [Fact]
    public async Task ReservationReplay_NeverAuthorizesASecondInference()
    {
        await Service.SaveConfigurationAsync(Tenant, Configuration());
        var request = Request();
        await Service.ReserveAsync(Tenant, request);
        var error = await Assert.ThrowsAsync<BudgetException>(() => Service.ReserveAsync(Tenant, request));
        Assert.Equal("request_already_reserved", error.Code);
        var changed = await Assert.ThrowsAsync<BudgetException>(() => Service.ReserveAsync(Tenant, request with { DeploymentId = "other" }));
        Assert.Equal("request_id_conflict", changed.Code);
        Assert.Equal(2, Assert.Single(await store.ListAsync<BudgetBalance>(Tenant, "balance")).ReservedUsd);
    }

    [Theory]
    [InlineData("Shared", 1)]
    [InlineData("PerMember", 2)]
    public async Task GroupAssignment_UsesExplicitSharedOrPerMemberBuckets(string mode, int buckets)
    {
        await Service.SaveConfigurationAsync(Tenant, Configuration(50, "Group", mode));
        await Service.ReserveAsync(Tenant, Request(groups: [Group]));
        await Service.ReserveAsync(Tenant, Request(Guid.NewGuid().ToString(), [Group]));
        var balances = await store.ListAsync<BudgetBalance>(Tenant, "balance");
        Assert.Equal(buckets, balances.Count);
        Assert.Equal(4, balances.Sum(b => b.ReservedUsd));
    }

    [Fact]
    public async Task SamePerMemberPolicyThroughTwoGroups_DoesNotDoubleCharge()
    {
        var config = Configuration(50, "Group");
        var other = Guid.NewGuid().ToString();
        config.Assignments.Add(new(Policy, "Group", other));
        config.Assignments.Add(new(Policy, "User", User));
        await Service.SaveConfigurationAsync(Tenant, config);
        var decision = await Service.ReserveAsync(Tenant, Request(groups: [Group, other]));
        Assert.Single(decision.Balances);
        Assert.Equal(2, decision.Balances[0].ReservedUsd);
    }

    [Fact]
    public async Task MonthlyAndWeeklyLimits_RejectAtomicallyWithoutPartialDebit()
    {
        var config = Configuration();
        var weekly = Guid.NewGuid().ToString();
        config.Policies.Add(new(weekly, "Weekly", 1m, "Weekly"));
        config.Assignments.Add(new(weekly, "User", User));
        await Service.SaveConfigurationAsync(Tenant, config);
        var error = await Assert.ThrowsAsync<BudgetException>(() => Service.ReserveAsync(Tenant, Request()));
        Assert.Equal("budget_exceeded", error.Code);
        Assert.NotNull(error.ResetsAt);
        Assert.Empty(await store.ListAsync<BudgetBalance>(Tenant, "balance"));
        Assert.Empty(await store.ListAsync<BudgetReservation>(Tenant, "reservation"));
    }

    [Fact]
    public async Task LateSettlement_ChargesOriginalPeriod_AndMissingSettlementNeverExpires()
    {
        await Service.SaveConfigurationAsync(Tenant, Configuration());
        var first = Request();
        await Service.ReserveAsync(Tenant, first);
        clock.Now = clock.Now.AddMonths(1);
        await Service.ReserveAsync(Tenant, Request());
        await Service.SettleAsync(Tenant, first.RequestId, new(new(100, 100)));
        var balances = (await store.ListAsync<BudgetBalance>(Tenant, "balance")).OrderBy(x => x.PeriodStart).ToArray();
        Assert.Equal(0.2m, balances[0].SpentUsd);
        Assert.Equal(0, balances[0].ReservedUsd);
        Assert.Equal(2, balances[1].ReservedUsd);
        clock.Now = clock.Now.AddYears(1);
        Assert.Equal(2, (await store.ListAsync<BudgetBalance>(Tenant, "balance")).Sum(b => b.ReservedUsd));
    }

    [Fact]
    public async Task Settlement_PinsPriceVersion_AndAccountsForCachedInput()
    {
        var saved = await Service.SaveConfigurationAsync(Tenant, Configuration());
        var request = Request();
        await Service.ReserveAsync(Tenant, request);
        saved.Models = [new("chat", "v2", 2000, 2000, 1000, 1000, 1000)];
        await Service.SaveConfigurationAsync(Tenant, saved);
        var settled = await Service.SettleAsync(Tenant, request.RequestId, new(new(100, 100, 50)));
        Assert.Equal(0.175m, settled.ActualUsd);
        Assert.Equal("v1", settled.Rate.Version);
    }

    [Fact]
    public async Task UnknownPricesAndIncompleteGroups_FailClosed()
    {
        await Service.SaveConfigurationAsync(Tenant, Configuration(50, "Group"));
        var request = Request(groups: [Group]);
        var groups = await Assert.ThrowsAsync<BudgetException>(() => Service.ReserveAsync(Tenant, request with { GroupsComplete = false }));
        Assert.Equal("group_membership_incomplete", groups.Code);
        var pricing = await Assert.ThrowsAsync<BudgetException>(() => Service.ReserveAsync(Tenant, request with { DeploymentId = "unknown" }));
        Assert.Equal("budget_price_missing", pricing.Code);
        Assert.Empty(await store.ListAsync<BudgetReservation>(Tenant, "reservation"));
    }

    [Fact]
    public async Task ApplicationToken_DoesNotImpersonateAUser()
    {
        await Service.SaveConfigurationAsync(Tenant, Configuration());
        var error = await Assert.ThrowsAsync<BudgetException>(() => Service.ReserveAsync(Tenant, Request(user: null)));
        Assert.Equal("budget_not_assigned", error.Code);
    }

    [Fact]
    public async Task SameUserAcrossApplications_SharesBudget_ButTenantsAreIsolated()
    {
        await Service.SaveConfigurationAsync(Tenant, Configuration());
        await Service.ReserveAsync(Tenant, Request());
        await Service.ReserveAsync(Tenant, Request() with { ClientAppId = Guid.NewGuid().ToString() });
        Assert.Equal(4, Assert.Single(await store.ListAsync<BudgetBalance>(Tenant, "balance")).ReservedUsd);
        var other = Guid.NewGuid().ToString();
        await Service.SaveConfigurationAsync(other, Configuration());
        await Service.ReserveAsync(other, Request());
        Assert.Equal(2, Assert.Single(await store.ListAsync<BudgetBalance>(other, "balance")).ReservedUsd);
    }

    [Theory]
    [InlineData("{\"messages\":[{\"role\":\"user\",\"content\":\"x\"}],\"stream\":true,\"max_tokens\":10}")]
    [InlineData("{\"messages\":[{\"role\":\"user\",\"content\":[{\"type\":\"image_url\"}]}],\"max_tokens\":10}")]
    [InlineData("{\"messages\":[{\"role\":\"user\",\"content\":\"x\"}],\"tools\":[],\"max_tokens\":10}")]
    [InlineData("{\"messages\":[{\"role\":\"user\",\"content\":\"x\"}],\"n\":2,\"max_tokens\":10}")]
    [InlineData("{\"messages\":[{\"role\":\"user\",\"content\":\"x\"}],\"max_tokens\":\"10\"}")]
    [InlineData("{\"messages\":[{\"role\":\"user\",\"content\":\"x\"}]}")]
    [InlineData("{\"messages\":[{\"role\":\"user\",\"content\":\"x\"}],\"max_tokens\":10,\"max_completion_tokens\":10}")]
    [InlineData("{\"messages\":[{\"role\":\"user\",\"content\":\"x\"}],\"max_tokens\":1001}")]
    public async Task UnsupportedOrUnboundedRequests_NeverReserve(string body)
    {
        await Service.SaveConfigurationAsync(Tenant, Configuration());
        await Assert.ThrowsAsync<BudgetException>(() => Service.ReserveAsync(Tenant, Request() with { RequestBody = JsonDocument.Parse(body).RootElement }));
        Assert.Empty(await store.ListAsync<BudgetReservation>(Tenant, "reservation"));
    }

    [Fact]
    public async Task TinyCosts_AreNotRoundedToZero()
    {
        var config = Configuration();
        config.Models = [new("chat", "v1", 0.01m, 0.01m, 0m, 1000, 1000)];
        await Service.SaveConfigurationAsync(Tenant, config);
        var request = Request();
        await Service.ReserveAsync(Tenant, request);
        var settled = await Service.SettleAsync(Tenant, request.RequestId, new(new(1, 1)));
        Assert.Equal(0.00000002m, settled.ActualUsd);
    }

    [Fact]
    public async Task UnderconfiguredModelBound_RecordsActualSpendAndOverrun()
    {
        await Service.SaveConfigurationAsync(Tenant, Configuration());
        var request = Request();
        await Service.ReserveAsync(Tenant, request);
        var settled = await Service.SettleAsync(Tenant, request.RequestId, new(new(2000, 2000)));
        Assert.True(settled.Overrun);
        Assert.Equal(4m, settled.ActualUsd);
    }

    [Fact]
    public async Task ConfigurationRequiresCurrentRevision_AndPreservesPeriod()
    {
        var saved = await Service.SaveConfigurationAsync(Tenant, Configuration());
        var stale = await Assert.ThrowsAsync<BudgetException>(() => Service.SaveConfigurationAsync(Tenant, Configuration()));
        Assert.Equal("configuration_changed", stale.Code);
        saved.Policies = [saved.Policies[0] with { Period = "Weekly" }];
        var period = await Assert.ThrowsAsync<BudgetException>(() => Service.SaveConfigurationAsync(Tenant, saved));
        Assert.Equal("immutable_period", period.Code);
    }

    [Theory]
    [InlineData("2028-02-29T23:59:59Z", "Monthly", "2028-02-01T00:00:00Z", "2028-03-01T00:00:00Z")]
    [InlineData("2026-10-11T23:59:59Z", "Weekly", "2026-10-05T00:00:00Z", "2026-10-12T00:00:00Z")]
    [InlineData("2026-10-12T00:00:00Z", "Weekly", "2026-10-12T00:00:00Z", "2026-10-19T00:00:00Z")]
    public void Periods_UseCalendarBoundaries(string time, string period, string start, string end)
    {
        var result = BudgetService.Period(DateTimeOffset.Parse(time), period);
        Assert.Equal(DateTimeOffset.Parse(start), result.Start);
        Assert.Equal(DateTimeOffset.Parse(end), result.End);
    }
}

internal sealed class BudgetClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-08T12:00:00Z");
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class MemoryBudgetStore : IBudgetStore
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly Dictionary<string, Dictionary<string, string>> tenants = [];

    public async Task<T> TransactAsync<T>(string tenant, Func<IBudgetTransaction, Task<T>> action, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!tenants.TryGetValue(tenant, out var documents)) tenants[tenant] = documents = [];
            var tx = new Transaction(documents);
            var result = await action(tx);
            foreach (var (id, json) in tx.Writes) documents[id] = json;
            return result;
        }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlyList<T>> ListAsync<T>(string tenant, string kind, CancellationToken ct = default) where T : BudgetDocument
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!tenants.TryGetValue(tenant, out var documents)) return [];
            return documents.Values.Select(json => JsonSerializer.Deserialize<T>(json, JsonConfig.Default)!)
                .Where(d => d.Kind == kind).ToList();
        }
        finally { gate.Release(); }
    }

    private sealed class Transaction(Dictionary<string, string> documents) : IBudgetTransaction
    {
        public Dictionary<string, string> Writes { get; } = [];
        public Task<T?> ReadAsync<T>(string id) where T : BudgetDocument => Task.FromResult(
            documents.TryGetValue(id, out var json) ? JsonSerializer.Deserialize<T>(json, JsonConfig.Default) : null);
        public void Write(BudgetDocument document) => Writes[document.Id] = JsonSerializer.Serialize(document, document.GetType(), JsonConfig.Default);
    }
}
