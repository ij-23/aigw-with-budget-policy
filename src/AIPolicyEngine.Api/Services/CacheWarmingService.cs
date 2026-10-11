using System.Text.Json;
using AIPolicyEngine.Api.Models;
using StackExchange.Redis;

namespace AIPolicyEngine.Api.Services;

/// <summary>
/// Warms the Redis cache from Postgres on startup.
/// If Redis is unavailable, logs a warning but does not fail startup.
/// If Postgres is unavailable, fails startup — no source of truth means no service.
/// </summary>
public sealed class CacheWarmingService : IHostedService
{
    private readonly PostgresPlanRepository _planRepo;
    private readonly PostgresClientRepository _clientRepo;
    private readonly PostgresPricingRepository _pricingRepo;
    private readonly PostgresUsagePolicyRepository _usagePolicyRepo;
    private readonly PostgresRoutingPolicyRepository _routingPolicyRepo;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CacheWarmingService> _logger;

    public CacheWarmingService(
        PostgresPlanRepository planRepo,
        PostgresClientRepository clientRepo,
        PostgresPricingRepository pricingRepo,
        PostgresUsagePolicyRepository usagePolicyRepo,
        PostgresRoutingPolicyRepository routingPolicyRepo,
        IConnectionMultiplexer redis,
        ILogger<CacheWarmingService> logger)
    {
        _planRepo = planRepo;
        _clientRepo = clientRepo;
        _pricingRepo = pricingRepo;
        _usagePolicyRepo = usagePolicyRepo;
        _routingPolicyRepo = routingPolicyRepo;
        _redis = redis;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Cache warming starting — loading configuration from Postgres");

        // Postgres must be available. If it throws, startup fails (by design).
        var plans = await _planRepo.GetAllAsync(ct);
        var clients = await _clientRepo.GetAllAsync(ct);
        var pricing = await _pricingRepo.GetAllAsync(ct);
        var usagePolicy = await _usagePolicyRepo.GetAsync("usage-policy", ct);
        var routingPolicies = await _routingPolicyRepo.GetAllAsync(ct);

        try
        {
            var db = _redis.GetDatabase();

            foreach (var plan in plans)
            {
                var json = JsonSerializer.Serialize(plan, JsonConfig.Default);
                await db.StringSetAsync(RedisKeys.Plan(plan.Id), json);
            }

            foreach (var client in clients)
            {
                var json = JsonSerializer.Serialize(client, JsonConfig.Default);
                await db.StringSetAsync(RedisKeys.Client(client.ClientAppId, client.TenantId), json);
            }

            foreach (var model in pricing)
            {
                var json = JsonSerializer.Serialize(model, JsonConfig.Default);
                await db.StringSetAsync(RedisKeys.Pricing(model.ModelId), json);
            }

            if (usagePolicy is not null)
            {
                var json = JsonSerializer.Serialize(usagePolicy, JsonConfig.Default);
                await db.StringSetAsync(RedisKeys.UsagePolicySettings, json);
            }

            foreach (var policy in routingPolicies)
            {
                var json = JsonSerializer.Serialize(policy, JsonConfig.Default);
                await db.StringSetAsync(RedisKeys.RoutingPolicy(policy.Id), json);
            }

            _logger.LogInformation(
                "Cache warming complete: {Plans} plans, {Clients} clients, {Pricing} pricing models, {RoutingPolicies} routing policies, usage-policy={HasUsagePolicy}",
                plans.Count, clients.Count, pricing.Count, routingPolicies.Count, usagePolicy is not null);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex,
                "Redis unavailable during cache warming — data loaded from Postgres but cache is cold. " +
                "Reads will fall back to Postgres until Redis recovers.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
