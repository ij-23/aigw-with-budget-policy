namespace AIPolicyEngine.Tests;
// Real database tests are mandatory in CI and opt-in for local unit-only runs.
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")))
            Skip = "Set TEST_POSTGRES_CONNECTION to run against PostgreSQL.";
    }
}
