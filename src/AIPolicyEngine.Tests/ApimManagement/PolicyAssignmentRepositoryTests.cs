using System.Text.Json;
using AIPolicyEngine.Api.Models.Apim;
using AIPolicyEngine.Api.Services;
using AIPolicyEngine.Api.Services.ApimManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
namespace AIPolicyEngine.Tests.ApimManagement;
public sealed class PolicyAssignmentRepositoryTests
{
    [Fact]
    public void BuildId_FormatsIdsPerSpec()
    {
        Assert.Equal("pa:azure-openai:_all", PolicyAssignment.BuildId("azure-openai", null));
        Assert.Equal("pa:azure-openai:chat", PolicyAssignment.BuildId("azure-openai", "chat"));
    }
    [PostgresFact]
    public async Task AssignmentCrud_RoundTripsScopedIdsAndParameters()
    {
        await using var source = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")!);
        var repo = new PostgresPolicyAssignmentRepository(new PostgresDocumentStore(source), NullLogger<PostgresPolicyAssignmentRepository>.Instance);
        var api = Guid.NewGuid().ToString();
        var assignment = new PolicyAssignment { ApiId = api, TemplateId = "template-a", Parameters = new() { ["audience"] = JsonSerializer.SerializeToElement("api://test") } };
        await repo.UpsertAsync(assignment);
        Assert.Equal($"pa:{api}:_all", assignment.Id);
        Assert.Equal("policy-assignment", assignment.PartitionKey);
        Assert.Equal("api://test", (await repo.GetAsync(api, null))!.Parameters["audience"].GetString());
        assignment.TemplateId = "template-b";
        await repo.UpsertAsync(assignment);
        Assert.Equal("template-b", (await repo.GetAsync(api, null))!.TemplateId);
        Assert.True(await repo.DeleteAsync(api, null));
        Assert.False(await repo.DeleteAsync(api, null));
        Assert.Null(await repo.GetAsync(api, null));
    }
}
