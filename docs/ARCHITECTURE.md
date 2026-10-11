# Architecture and request flow

This POC gives an Entra user or group a weekly or monthly allowance, such as **$50 per calendar month**, for AI requests through Azure API Management (APIM). The policy engine decides whether a request fits the allowance. PostgreSQL records the reservation and final cost. Azure Managed Redis supports access checks, rate controls and activity views.

## Solution architecture

```mermaid
flowchart TB
    user["Test user / AI application"]
    admin["Budget administrator"]
    entra["Microsoft Entra ID\nUser tokens, groups and application roles"]
    apim["Azure API Management\nValidate JWT · precheck · reserve · settle"]
    foundry["Microsoft Foundry\nApproved model deployment"]
    subgraph vnet["Virtual network"]
        subgraph apps["Container Apps subnet"]
            engine["Policy engine + administrator dashboard\nAzure Container App · managed identity"]
        end
        subgraph endpoints["Private endpoints subnet"]
            rpe["Redis private endpoint"]
            cpe["PostgreSQL private endpoint"]
            kpe["Key Vault private endpoint"]
        end
    end
    redis["Azure Managed Redis\nAccess cache · rate counters · recent activity"]
    postgresql["PostgreSQL\nPolicies · assignments · prices\nUSD reservations and durable spending"]
    kv["Key Vault\nLab bootstrap credentials"]
    acr["Azure Container Registry\nPolicy engine image"]
    monitor["Azure Monitor\nLog Analytics + Application Insights"]
    user -->|Sign in| entra
    user -->|Bearer token + text chat request| apim
    apim -->|Gateway managed identity · HTTPS| engine
    apim -->|Gateway managed identity · allowed request| foundry
    admin -->|Entra sign in · HTTPS| engine
    engine -->|TLS · engine managed identity| rpe
    rpe --> redis
    engine -->|TLS · database credential secret| cpe
    cpe --> postgresql
    kpe --> kv
    engine -.->|Managed identity pulls image| acr
    engine -.->|Application telemetry| monitor
    apim -.->|Gateway diagnostics| monitor
    foundry -.->|Model diagnostics| monitor
    redis -.->|Metrics + connection events| monitor
    postgresql -.->|Data-plane diagnostics| monitor
```

Private DNS zones resolve the Redis, PostgreSQL and Key Vault hostnames to their private endpoints. APIM and the engine expose HTTPS endpoints in this POC; caller and gateway authorization protect the application. Foundry's model endpoint is reached by APIM with its managed identity. Key Vault supports lab provisioning; it is not called for every inference request. Terraform writes lab credentials through the ARM control plane, while their data-plane retrieval requires private connectivity.

## What happens for one request

```mermaid
sequenceDiagram
    autonumber
    actor U as Test user
    participant A as APIM
    participant E as Policy engine
    participant R as Azure Managed Redis
    participant C as PostgreSQL
    participant F as Foundry model
    U->>A: JWT + non-streaming text chat + output cap
    A->>A: Validate issuer, audience, user and groups
    A->>E: Precheck application access, routing and rate limits
    E->>R: Read cached access configuration and rate counters
    opt Configuration cache miss
        E->>C: Read configuration
        E->>R: Populate configuration cache
    end
    A->>E: Reserve dollars for validated user / groups
    E->>C: Read allowances and prices, then atomically reserve maximum cost
    alt Allowance cannot cover spent + reserved + maximum
        C-->>E: Deny reservation
        E-->>A: budget_exceeded
        A-->>U: HTTP 429 - no model call
    else Reservation accepted
        C-->>E: Reservation ID and immutable price snapshot
        E-->>A: Authorized
        A->>F: One model call with gateway managed identity
        F-->>A: Text response and actual token usage
        A->>E: Settle reservation using actual usage
        E->>C: Record actual cost and release unused reservation
        E-->>A: Settlement result
        A-->>U: Model response
    end
    Note over E,C: Uncertain model outcomes keep funds reserved until verified reconciliation
```

APIM uses separate calls for access precheck, dollar authorization and settlement. Redis is contacted during the precheck and supporting cache/activity operations. Every request reaching dollar authorization contacts PostgreSQL, even with a warm Redis cache. Successful responses trigger a second PostgreSQL accounting operation. The dollar ledger does not rely on Redis counters, cache TTLs or distributed locks.

## Example: a $50 monthly allowance

A user starts with $0 spent and $0 reserved. Suppose a request's conservative maximum cost is $0.06. PostgreSQL first records that reservation. If actual usage costs $0.002, settlement increases spending to $0.002 and releases the $0.06 reservation. Remaining allowance is $49.998. These amounts illustrate the flow; actual charges depend on the configured model prices and reported usage.

A later request is blocked when `spent + outstanding reservations + maximum request cost > $50`. Blocking can occur before actual spending reaches $50 because the remaining allowance must cover the worst allowed request. Monthly windows start at 00:00 UTC on the first of the month; weekly windows start Monday at 00:00 UTC. Policies can apply to a user, a shared group bucket, or a separate allowance per member.

## Redis deployment options

| Option | Terraform configuration | Result |
|---|---|---|
| Create Azure Managed Redis (default) | `create_redis = true` | Creates a Managed Redis instance, private endpoint, private DNS zone/link, diagnostics and engine identity data access. |
| Bring existing Redis | `create_redis = false` plus `existing_redis_connection_string` | Creates no Redis resources. Customer provides a reachable cache, DNS and engine data access. |
| Run without any Redis service | Not supported by this engine | Access checks, rate counters and activity services currently require Redis. Terraform rejects an empty existing connection when creation is disabled. |

`redis_location` defaults to the main deployment region; override it when subscription restrictions or capacity require another region. The private endpoint remains in the engine VNet region. Cross-region cache access adds latency and may add network charges.

The default SKU is `Balanced_B0`, with high availability disabled for the POC. Set `redis_high_availability_enabled = true` for replicas. The database uses TLS, disabled access keys, Entra authentication, `NoCluster` for compatibility and `NoEviction` to avoid silently evicting active counters. Monitor cache memory and size it for the workload. The engine automatically refreshes Entra authentication through its Redis client integration.

For an existing Managed Redis instance, use the ordinary service hostname and TLS port, for example `mycache.swedencentral.redis.azure.net:10000,ssl=True,abortConnect=False`. Grant the engine managed identity **Redis data access** through the cache's authentication settings; management-plane Contributor alone is insufficient. Use a non-clustered cache for the supported POC configuration. Password-backed existing Redis is also accepted when a password is supplied in the connection string.

## Where to see usage

1. Open the `dashboard_url` Terraform output and sign in as the configured administrator.
2. Open **Budgets**, enter the tenant ID and select **Load budgets**.
3. Inspect the $50 policy, its user/group assignment, spending, reserved amounts and remaining allowance.
4. Run `node scripts/lab/e2e-budget.mjs --device-code --verify-block` for a real user-authenticated model call and budget-block verification. Its temporary reduced allowance is restored to $50.

PostgreSQL contains the authoritative financial records. Redis metrics show cache health and activity, not the user's durable USD allowance. Azure Cost Management shows Azure infrastructure/invoice costs, which are separate from this application's price-book accounting.

For resource purposes and limitations, see [Solution](SOLUTION.md). For deployment, existing-platform prerequisites and interactive testing, see [Azure budget lab](AZURE_BUDGET_LAB.md).
