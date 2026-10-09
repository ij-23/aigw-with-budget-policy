using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIPolicyEngine.Api.Models;

namespace AIPolicyEngine.Api.Services.Budgets;

public sealed class BudgetService(IBudgetStore store, TimeProvider clock)
{
    public Task<BudgetConfiguration> GetConfigurationAsync(string tenant, CancellationToken ct = default) =>
        store.TransactAsync(tenant, async tx => await tx.ReadAsync<BudgetConfiguration>("config") ?? NewConfiguration(), ct);

    public Task<BudgetConfiguration> SaveConfigurationAsync(string tenant, BudgetConfiguration config, CancellationToken ct = default)
    {
        ValidateConfiguration(config);
        config.Policies = config.Policies.Select(p => p with { Id = Guid.Parse(p.Id).ToString("D") }).ToList();
        config.Assignments = config.Assignments.Select(a => a with
        {
            PolicyId = Guid.Parse(a.PolicyId).ToString("D"), SubjectId = Guid.Parse(a.SubjectId).ToString("D")
        }).Distinct().ToList();
        return store.TransactAsync(tenant, async tx =>
        {
            var previous = await tx.ReadAsync<BudgetConfiguration>("config") ?? NewConfiguration();
            if (previous.Revision != config.Revision)
                throw new BudgetException(409, "configuration_changed", "Reload the budget configuration before saving.");
            foreach (var policy in config.Policies)
                if (previous.Policies.Find(p => p.Id == policy.Id) is { } old && old.Period != policy.Period)
                    throw new BudgetException(400, "immutable_period", "Create a new policy to change its reset period.");
            var saved = new BudgetConfiguration
            {
                Id = "config", Kind = "configuration", Revision = previous.Revision + 1,
                Policies = config.Policies, Assignments = config.Assignments, Models = config.Models
            };
            tx.Write(saved);
            return saved;
        }, ct);
    }

    public Task<BudgetDecision> ReserveAsync(string tenant, BudgetReserveRequest request, CancellationToken ct = default)
    {
        ValidateIdentity(tenant, request);
        request = request with
        {
            RequestId = Guid.Parse(request.RequestId).ToString("D"),
            ClientAppId = Guid.Parse(request.ClientAppId).ToString("D"),
            UserId = request.UserId is null ? null : Guid.Parse(request.UserId).ToString("D"),
            GroupIds = request.GroupIds.Select(g => Guid.Parse(g).ToString("D")).Distinct().Order().ToArray()
        };
        var now = clock.GetUtcNow();
        var id = "reservation:" + request.RequestId;
        var fingerprint = Hash(JsonSerializer.Serialize(request, JsonConfig.Default));
        return store.TransactAsync(tenant, async tx =>
        {
            // A replay never issues a second forwarding authorization: one reservation = one inference.
            if (await tx.ReadAsync<BudgetReservation>(id) is { } existing)
                throw new BudgetException(409, existing.Fingerprint == fingerprint ? "request_already_reserved" : "request_id_conflict",
                    "This request ID has already been reserved. Do not forward it again.");
            var config = await tx.ReadAsync<BudgetConfiguration>("config") ??
                throw new BudgetException(403, "budget_not_assigned", "No budget is configured for this tenant.");
            var matches = Resolve(config, request);
            if (matches.Count == 0)
                throw new BudgetException(403, "budget_not_assigned", "No active budget is assigned to this consumer.");
            if (matches.Count > 20)
                throw new BudgetException(400, "too_many_budgets", "A request can apply to at most 20 budget buckets.");
            var rate = config.Models.Find(m => m.DeploymentId == request.DeploymentId) ??
                throw new BudgetException(403, "budget_price_missing", "This deployment has no approved budget price.");
            var outputLimit = ValidateTextChat(request.RequestBody, rate);
            // Reserve the full configured backend input limit, not an unsafe prompt estimate.
            var maximum = Money(rate.MaxInputTokens * rate.InputUsdPerMillion / 1_000_000m
                + outputLimit * rate.OutputUsdPerMillion / 1_000_000m);
            var balances = new List<BudgetBalance>();
            foreach (var (policy, subject) in matches)
            {
                var (start, end) = Period(now, policy.Period);
                var balanceId = "balance:" + Hash($"{policy.Id}|{subject}|{start:O}");
                var balance = await tx.ReadAsync<BudgetBalance>(balanceId) ?? new BudgetBalance
                {
                    Id = balanceId, Kind = "balance", PolicyId = policy.Id, Subject = subject,
                    PeriodStart = start, ResetsAt = end
                };
                balance.LimitUsd = policy.LimitUsd;
                if (balance.SpentUsd + balance.ReservedUsd + maximum > policy.LimitUsd)
                    throw new BudgetException(429, "budget_exceeded", "Remaining budget cannot cover this request's maximum cost.", end);
                balance.ReservedUsd += maximum;
                balances.Add(balance);
            }
            foreach (var balance in balances) tx.Write(balance);
            tx.Write(new BudgetReservation
            {
                Id = id, Kind = "reservation", Fingerprint = fingerprint, ClientAppId = request.ClientAppId,
                UserId = request.UserId, DeploymentId = request.DeploymentId, Rate = rate,
                BalanceIds = balances.Select(b => b.Id).ToList(), ReservedUsd = maximum,
                MaxOutputTokens = outputLimit, CreatedAt = now
            });
            return new BudgetDecision(request.RequestId, maximum, balances);
        }, ct);
    }

    public Task<BudgetReservation> SettleAsync(string tenant, string requestId, BudgetSettleRequest request,
        CancellationToken ct = default) => store.TransactAsync(tenant, async tx =>
    {
        if (!Guid.TryParse(requestId, out var requestGuid))
            throw new BudgetException(400, "invalid_request_id", "Request ID must be a UUID.");
        var reservation = await tx.ReadAsync<BudgetReservation>("reservation:" + requestGuid.ToString("D")) ??
            throw new BudgetException(404, "reservation_not_found", "No budget reservation exists for this request.");
        var usage = request.Usage;
        if (usage is null || usage.PromptTokens < 0 || usage.CompletionTokens < 0 ||
            usage.CachedInputTokens < 0 || usage.CachedInputTokens > usage.PromptTokens)
            throw new BudgetException(400, "invalid_usage", "Usage must contain nonnegative, consistent token counts.");
        if (reservation.SettledAt is not null)
        {
            if (reservation.Usage != usage)
                throw new BudgetException(409, "settlement_conflict", "Reservation was already settled with different usage.");
            return reservation;
        }
        var rate = reservation.Rate;
        var actual = Money(((usage.PromptTokens - usage.CachedInputTokens) * rate.InputUsdPerMillion
            + usage.CachedInputTokens * rate.CachedInputUsdPerMillion
            + usage.CompletionTokens * rate.OutputUsdPerMillion) / 1_000_000m);
        // Always account for actual usage, even if an incorrectly configured backend exceeded its bounds.
        foreach (var id in reservation.BalanceIds)
        {
            var balance = await tx.ReadAsync<BudgetBalance>(id) ??
                throw new BudgetException(503, "ledger_inconsistent", "A reserved budget balance is missing.");
            balance.ReservedUsd -= reservation.ReservedUsd;
            balance.SpentUsd += actual;
            tx.Write(balance);
        }
        reservation.ActualUsd = actual;
        reservation.Usage = usage;
        reservation.SettledAt = clock.GetUtcNow();
        reservation.ReconciliationNote = request.Note;
        reservation.Overrun = actual > reservation.ReservedUsd || usage.PromptTokens > rate.MaxInputTokens ||
            usage.CompletionTokens > reservation.MaxOutputTokens;
        if (reservation.Overrun)
        {
            // Quarantine an unsafe price/bound configuration before another request can use it.
            var config = await tx.ReadAsync<BudgetConfiguration>("config");
            if (config is not null && config.Models.RemoveAll(m => m == rate) > 0)
            {
                config.Revision++;
                tx.Write(config);
            }
        }
        tx.Write(reservation);
        return reservation;
    }, ct);

    public static (DateTimeOffset Start, DateTimeOffset End) Period(DateTimeOffset now, string period)
    {
        var utc = now.UtcDateTime;
        if (period == "Monthly")
        {
            var start = new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero);
            return (start, start.AddMonths(1));
        }
        if (period != "Weekly") throw new BudgetException(400, "invalid_period", "Period must be Weekly or Monthly.");
        var monday = new DateTimeOffset(utc.Date, TimeSpan.Zero).AddDays(-((int)utc.DayOfWeek + 6) % 7);
        return (monday, monday.AddDays(7));
    }

    private static List<(BudgetPolicy Policy, string Subject)> Resolve(BudgetConfiguration config, BudgetReserveRequest request)
    {
        if (!request.GroupsComplete && config.Assignments.Any(a => a.SubjectType == "Group"))
            throw new BudgetException(403, "group_membership_incomplete", "Complete verified group membership is required.");
        var result = new Dictionary<string, (BudgetPolicy, string)>();
        foreach (var assignment in config.Assignments)
        {
            var matched = assignment.SubjectType switch
            {
                "User" => request.UserId == assignment.SubjectId,
                "Application" => request.ClientAppId == assignment.SubjectId,
                "Group" => request.UserId is not null && request.GroupIds.Contains(assignment.SubjectId, StringComparer.Ordinal),
                _ => false
            };
            if (!matched) continue;
            var policy = config.Policies.Find(p => p.Id == assignment.PolicyId && p.Enabled);
            if (policy is null) continue;
            var subject = assignment.SubjectType == "Application" ? "application:" + request.ClientAppId
                : assignment.SubjectType == "Group" && assignment.GroupMode == "Shared" ? "group:" + assignment.SubjectId
                : "user:" + request.UserId;
            result[$"{policy.Id}|{subject}"] = (policy, subject);
        }
        return result.Values.ToList();
    }

    private static int ValidateTextChat(JsonElement body, BudgetModelRate rate)
    {
        if (body.ValueKind != JsonValueKind.Object)
            throw new BudgetException(400, "unsupported_request", "A text Chat Completions JSON object is required.");
        string[] allowed = ["model", "messages", "max_tokens", "max_completion_tokens", "temperature", "top_p",
            "frequency_penalty", "presence_penalty", "stop", "seed", "user", "stream", "n"];
        if (body.EnumerateObject().Any(p => !allowed.Contains(p.Name)) ||
            body.EnumerateObject().Select(p => p.Name).Distinct().Count() != body.EnumerateObject().Count() ||
            (body.TryGetProperty("stream", out var stream) && stream.ValueKind != JsonValueKind.False) ||
            (body.TryGetProperty("n", out var n) && (n.ValueKind != JsonValueKind.Number || !n.TryGetInt32(out var count) || count != 1)))
            throw new BudgetException(400, "unsupported_request", "Budget requests currently support non-streaming text chat with one completion and no tools.");
        if (!body.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array || messages.GetArrayLength() == 0)
            throw new BudgetException(400, "unsupported_request", "Nonempty text messages are required.");
        foreach (var message in messages.EnumerateArray())
            if (message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.String || !message.TryGetProperty("role", out var role) ||
                role.ValueKind != JsonValueKind.String || role.GetString() is not ("system" or "developer" or "user" or "assistant") ||
                message.EnumerateObject().Any(p => p.Name is not ("role" or "content")))
                throw new BudgetException(400, "unsupported_request", "Only plain text system, developer, user and assistant messages are supported.");
        var hasModern = body.TryGetProperty("max_completion_tokens", out var modern);
        var hasLegacy = body.TryGetProperty("max_tokens", out var legacy);
        var output = hasModern ? modern : legacy;
        if (hasModern == hasLegacy || output.ValueKind != JsonValueKind.Number || !output.TryGetInt32(out var limit) || limit <= 0 || limit > rate.MaxOutputTokens)
            throw new BudgetException(400, "output_limit_required", "Specify exactly one positive max_tokens or max_completion_tokens within the approved model limit.");
        return limit;
    }

    private static void ValidateIdentity(string tenant, BudgetReserveRequest request)
    {
        if (!Guid.TryParse(tenant, out _) || !Guid.TryParse(request.ClientAppId, out _) ||
            !Guid.TryParse(request.RequestId, out _) || (request.UserId is not null && !Guid.TryParse(request.UserId, out _)) ||
            request.GroupIds is null || request.GroupIds.Length > 200 || request.GroupIds.Any(g => !Guid.TryParse(g, out _)))
            throw new BudgetException(400, "invalid_identity", "Tenant, request, application, user and group IDs must be UUIDs.");
    }

    private static void ValidateConfiguration(BudgetConfiguration config)
    {
        if (config.Policies is null || config.Assignments is null || config.Models is null ||
            config.Policies.Count > 100 || config.Assignments.Count > 500 || config.Models.Count > 100)
            throw new BudgetException(400, "invalid_configuration", "Configuration exceeds supported size limits.");
        if (config.Policies.Any(p => !Guid.TryParse(p.Id, out _) || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 100 ||
                p.LimitUsd <= 0 || p.LimitUsd > 1_000_000 || p.Period is not ("Weekly" or "Monthly")) ||
            config.Policies.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != config.Policies.Count)
            throw new BudgetException(400, "invalid_policy", "Policies require unique UUIDs, a name, a positive USD limit and a Weekly/Monthly period.");
        if (config.Assignments.Any(a => !config.Policies.Any(p => string.Equals(p.Id, a.PolicyId, StringComparison.OrdinalIgnoreCase)) || !Guid.TryParse(a.SubjectId, out _) ||
                a.SubjectType is not ("User" or "Group" or "Application") || a.GroupMode is not ("PerMember" or "Shared")))
            throw new BudgetException(400, "invalid_assignment", "Assignments require an existing policy and a valid user, group or application UUID.");
        if (config.Models.Any(m => string.IsNullOrWhiteSpace(m.DeploymentId) || m.DeploymentId.Length > 200 ||
                string.IsNullOrWhiteSpace(m.Version) || m.Version.Length > 100 ||
                m.InputUsdPerMillion <= 0 || m.OutputUsdPerMillion <= 0 || m.CachedInputUsdPerMillion < 0 ||
                m.CachedInputUsdPerMillion > m.InputUsdPerMillion || m.InputUsdPerMillion > 100_000 || m.OutputUsdPerMillion > 100_000 ||
                m.MaxInputTokens is <= 0 or > 10_000_000 || m.MaxOutputTokens is <= 0 or > 1_000_000) ||
            config.Models.Select(m => m.DeploymentId).Distinct().Count() != config.Models.Count)
            throw new BudgetException(400, "invalid_pricing", "Models require unique deployments, versioned positive prices and valid input/output limits.");
    }

    private static BudgetConfiguration NewConfiguration() => new() { Id = "config", Kind = "configuration" };
    private static decimal Money(decimal value) => Math.Ceiling(value * 1_000_000_000m) / 1_000_000_000m;
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
