-- Idempotent POC schema. No TTL or automatic deletion of financial records.
CREATE TABLE IF NOT EXISTS configuration_documents (
    scope text NOT NULL,
    id text NOT NULL,
    body jsonb NOT NULL,
    PRIMARY KEY (scope, id)
);
CREATE TABLE IF NOT EXISTS budget_documents (
    tenant_id text NOT NULL,
    id text NOT NULL,
    kind text NOT NULL,
    body jsonb NOT NULL,
    PRIMARY KEY (tenant_id, id)
);
CREATE INDEX IF NOT EXISTS budget_documents_kind ON budget_documents(tenant_id, kind, id);
CREATE TABLE IF NOT EXISTS audit_documents (
    customer_key text NOT NULL,
    id text NOT NULL,
    body jsonb NOT NULL,
    PRIMARY KEY (customer_key, id)
);
CREATE INDEX IF NOT EXISTS audit_documents_period ON audit_documents((body->>'billingPeriod'));
CREATE TABLE IF NOT EXISTS billing_summaries (
    customer_key text NOT NULL,
    id text NOT NULL,
    body jsonb NOT NULL,
    PRIMARY KEY (customer_key, id)
);
CREATE INDEX IF NOT EXISTS billing_summaries_period ON billing_summaries((body->>'billingPeriod'));
-- Marks which audit requests have already contributed to summaries; retries cannot double count.
CREATE TABLE IF NOT EXISTS billing_processed_requests (
    customer_key text NOT NULL,
    request_id text NOT NULL,
    PRIMARY KEY (customer_key, request_id)
);
