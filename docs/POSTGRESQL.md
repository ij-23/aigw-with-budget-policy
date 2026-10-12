# PostgreSQL storage

The POC uses Azure Database for PostgreSQL Flexible Server. A single `aipolicy` database stores the application data. Terraform defaults to `B_Standard_B1ms`, 32 GiB storage, seven-day backups, no high availability, and Sweden Central. Private endpoint networking keeps public database access disabled.

If Azure returns `RegionalAllocationFailed`, choose another available compute size through `postgresql_sku_name` and retry Terraform. For example, `GP_Standard_D2ds_v5` uses a different compute family from the burstable default and costs more. SKU catalog availability does not guarantee live capacity. Azure selects the availability zone; Terraform preserves that placement on subsequent applies.

## Schema

The application applies [001_initial.sql](../src/AIPolicyEngine.Api/Database/001_initial.sql) on startup under a database advisory lock. Creating or reusing a database requires a database user with schema creation and CRUD privileges.

| Table | Purpose |
|---|---|
| `configuration_documents` | Plans, application assignments, access profiles, prices, routing and APIM assignments |
| `budget_documents` | Tenant budget policies, assignments, price snapshots, balances and reservations |
| `audit_documents` | Durable request audit records |
| `billing_summaries` | Monthly application/model analytics |
| `billing_processed_requests` | Prevent duplicate audit retries from incrementing summaries twice |

JSONB retains the existing API document shapes while relational primary keys identify each record. Money is serialized from .NET decimal values and read back as decimal; arithmetic never uses floating-point database conversion. Financial records have no automatic expiry. The dollar ledger remains independent of the asynchronous analytics pipeline.

## Atomic admission

Every budget configuration edit, reservation, settlement and reconciliation starts a PostgreSQL transaction using ReadCommitted isolation. Before reading, it acquires `pg_advisory_xact_lock(hashtextextended('budget:' + tenantId, 0))`. Waiting transactions read the previous committed state after obtaining the lock. All matching user/group balances and the reservation commit together. A callback failure rolls back; a connection or uncertain commit failure cannot return successful authorization. Duplicate request IDs still cannot authorize another inference.

The lock is scoped to a database transaction and automatically released. It also guards records that do not exist yet. A five-second lock timeout and bounded retries return HTTP 503 if contention persists. Different tenants have separate locks; a hash collision would serialize two tenants without sharing data. All financial writers must use this store and the primary database. Direct manual financial updates bypass its consistency rules.

This is a POC design that favors correctness and straightforward deployment. Tenant-wide serialization, 500-record dashboard limits, database growth, archival, restore procedures and managed identity database authentication should be reviewed for production.

## Creation and reuse

```hcl
location = "swedencentral"
create_postgresql = true
postgresql_sku_name = "B_Standard_B1ms"
postgresql_storage_mb = 32768
```

To reuse a server, set `create_postgresql=false`, `existing_postgresql_id`, and a sensitive `existing_postgresql_connection_string` in an ignored variable file. Include `SSL Mode=VerifyFull`. Set `create_postgresql_private_endpoint=false` when private connectivity already exists. The database user must have DDL/CRUD privileges; creation of the actual database and network/DNS readiness remain the customer's responsibility for an existing server.

The newly created server uses a generated database administrator password for the POC, stored in ignored Terraform state and a Container Apps secret. This differs from Redis, which uses the engine managed identity. Production hardening should introduce a dedicated least-privilege application database role or Entra database authentication.

## Run database tests

Use a disposable PostgreSQL 17 database:

```sh
export TEST_POSTGRES_CONNECTION='Host=localhost;Database=aipolicy_test;Username=budgettest;Password=your-test-password'
dotnet test src/AIPolicyEngine.Tests/AIPolicyEngine.Tests.csproj -p:SkipSpaBuild=true
```

CI starts PostgreSQL 17 and supplies this variable automatically. Without it, local database integration tests are skipped explicitly.
