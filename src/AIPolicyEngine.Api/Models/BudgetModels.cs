using System.Text.Json;

namespace AIPolicyEngine.Api.Models;

public class BudgetDocument
{
    public string Id { get; set; } = "";
    public string PartitionKey { get; set; } = "";
    public string Kind { get; set; } = "";
}

public sealed class BudgetConfiguration : BudgetDocument
{
    public int Revision { get; set; }
    public List<BudgetPolicy> Policies { get; set; } = [];
    public List<BudgetAssignment> Assignments { get; set; } = [];
    public List<BudgetModelRate> Models { get; set; } = [];
}

public sealed record BudgetPolicy(string Id, string Name, decimal LimitUsd = 50m,
    string Period = "Monthly", bool Enabled = true);

// User subjects are Entra object IDs. Application subjects are client IDs.
// Group PerMember assignments share a user's policy bucket across all matching groups.
public sealed record BudgetAssignment(string PolicyId, string SubjectType, string SubjectId,
    string GroupMode = "PerMember");

// Separate, explicit price book for hard budgets. Never fall back to guessed/zero prices.
// MaxInputTokens must be >= the backend model's hard input/context limit.
public sealed record BudgetModelRate(string DeploymentId, string Version,
    decimal InputUsdPerMillion, decimal OutputUsdPerMillion, decimal CachedInputUsdPerMillion,
    int MaxInputTokens, int MaxOutputTokens);

public sealed class BudgetBalance : BudgetDocument
{
    public string PolicyId { get; set; } = "";
    public string Subject { get; set; } = "";
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset ResetsAt { get; set; }
    public decimal LimitUsd { get; set; }
    public decimal SpentUsd { get; set; }
    public decimal ReservedUsd { get; set; }
    public decimal RemainingUsd => Math.Max(0, LimitUsd - SpentUsd - ReservedUsd);
}

public sealed class BudgetReservation : BudgetDocument
{
    public string Fingerprint { get; set; } = "";
    public string ClientAppId { get; set; } = "";
    public string? UserId { get; set; }
    public string DeploymentId { get; set; } = "";
    public BudgetModelRate Rate { get; set; } = null!;
    public List<string> BalanceIds { get; set; } = [];
    public decimal ReservedUsd { get; set; }
    public decimal? ActualUsd { get; set; }
    public int MaxOutputTokens { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SettledAt { get; set; }
    public BudgetUsage? Usage { get; set; }
    public string? ReconciliationNote { get; set; }
    public bool Overrun { get; set; }
}

public sealed record BudgetReserveRequest(string RequestId, string ClientAppId, string? UserId,
    string[] GroupIds, bool GroupsComplete, string DeploymentId, JsonElement RequestBody);
public sealed record BudgetUsage(
    [property: System.Text.Json.Serialization.JsonRequired] long PromptTokens,
    [property: System.Text.Json.Serialization.JsonRequired] long CompletionTokens,
    long CachedInputTokens = 0);
public sealed record BudgetSettleRequest(BudgetUsage Usage, string? Note = null);
public sealed record BudgetDecision(string ReservationId, decimal ReservedUsd,
    IReadOnlyList<BudgetBalance> Balances);

public sealed class BudgetException(int status, string code, string message,
    DateTimeOffset? resetsAt = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public DateTimeOffset? ResetsAt { get; } = resetsAt;
}
