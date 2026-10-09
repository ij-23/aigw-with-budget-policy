using System.Net;
using System.Net.Http.Json;
using AIPolicyEngine.Api.Models;
using AIPolicyEngine.Api.Services;
using AIPolicyEngine.Api.Services.Budgets;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static AIPolicyEngine.Tests.Budgets.BudgetServiceTests;

namespace AIPolicyEngine.Tests.Budgets;

public sealed class BudgetEndpointTests
{
    [Fact]
    public async Task ConfigureReserveSettle_EnforcesAllowance_AndReturnsResetTime()
    {
        using var factory = new ChargebackApiFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IBudgetStore>(new MemoryBudgetStore());
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test");
        var prefix = $"/api/budgets/{Tenant}";
        var config = await client.PutAsJsonAsync(prefix + "/configuration", Configuration(2));
        Assert.Equal(HttpStatusCode.OK, config.StatusCode);
        var request = Request();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(prefix + "/reserve", request)).StatusCode);
        var rejected = await client.PostAsJsonAsync(prefix + "/reserve", Request());
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Contains("budget_exceeded", await rejected.Content.ReadAsStringAsync());
        var settle = await client.PostAsJsonAsync(prefix + $"/reservations/{request.RequestId}/settle", new BudgetSettleRequest(new(100, 100)));
        Assert.Equal(HttpStatusCode.OK, settle.StatusCode);
        var balances = await client.GetFromJsonAsync<List<BudgetBalance>>(prefix + "/balances", JsonConfig.Default);
        Assert.Equal(0.2m, Assert.Single(balances!).SpentUsd);
        // Same request ID cannot forward another backend call after settlement either.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(prefix + "/reserve", request)).StatusCode);
    }

    [Theory]
    [InlineData("configuration")]
    [InlineData("balances")]
    [InlineData("reservations")]
    public async Task BudgetAdministration_RequiresAuthentication(string route)
    {
        using var factory = new ChargebackApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/budgets/{Tenant}/{route}")).StatusCode);
    }

    [Fact]
    public async Task MissingUsageCounts_CannotRefundReservation()
    {
        using var factory = new ChargebackApiFactory();
        var store = new MemoryBudgetStore();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IBudgetStore>(store)));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test");
        var prefix = $"/api/budgets/{Tenant}";
        await client.PutAsJsonAsync(prefix + "/configuration", Configuration());
        var request = Request();
        await client.PostAsJsonAsync(prefix + "/reserve", request);
        var response = await client.PostAsJsonAsync(prefix + $"/reservations/{request.RequestId}/settle", new { usage = new { } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2m, Assert.Single(await store.ListAsync<BudgetBalance>(Tenant, "balance")).ReservedUsd);
    }

    [Fact]
    public async Task TokenAllowance_CanBeDisabledWithoutDisablingDeploymentAccess()
    {
        using var factory = new ChargebackApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test");
        var plans = factory.Services.GetRequiredService<IRepository<PlanData>>();
        var clients = factory.Services.GetRequiredService<IRepository<ClientPlanAssignment>>();
        await plans.UpsertAsync(new() { Id = "dollars", EnforceTokenQuota = false, MonthlyTokenQuota = 0, AllowedDeployments = ["chat"] });
        await clients.UpsertAsync(new() { Id = $"{Client}:{Tenant}", ClientAppId = Client, TenantId = Tenant, PlanId = "dollars" });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/precheck/{Client}/{Tenant}?deploymentId=chat")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/precheck/{Client}/{Tenant}?deploymentId=unapproved")).StatusCode);
    }
}
