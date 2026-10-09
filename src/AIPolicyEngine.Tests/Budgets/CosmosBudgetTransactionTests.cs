using System.Net;
using System.Text.Json;
using AIPolicyEngine.Api.Models;
using AIPolicyEngine.Api.Services.Budgets;
using Microsoft.Azure.Cosmos;
using NSubstitute;

namespace AIPolicyEngine.Tests.Budgets;

public sealed class CosmosBudgetTransactionTests
{
    [Fact]
    public async Task Batch_PreservesDerivedFields_AndGuardsUnchangedConfiguration()
    {
        var container = Substitute.For<Container>();
        var batch = Substitute.For<TransactionalBatch>();
        container.CreateTransactionalBatch(Arg.Any<PartitionKey>()).Returns(batch);
        var response = Substitute.For<TransactionalBatchResponse>();
        response.IsSuccessStatusCode.Returns(true);
        batch.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(response);
        var configResponse = Substitute.For<ItemResponse<BudgetConfiguration>>();
        configResponse.Resource.Returns(new BudgetConfiguration { Id = "config", Kind = "configuration", Revision = 42 });
        configResponse.ETag.Returns("config-etag");
        container.ReadItemAsync<BudgetConfiguration>("config", Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>()).Returns(configResponse);
        container.ReadItemAsync<BudgetBalance>("balance", Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>())
            .Returns<Task<ItemResponse<BudgetBalance>>>(_ => throw new CosmosException("missing", HttpStatusCode.NotFound, 0, "", 0));
        var tx = new CosmosBudgetStore.Transaction(container, "budget:tenant", default);
        await tx.ReadAsync<BudgetConfiguration>("config");
        await tx.ReadAsync<BudgetBalance>("balance");
        tx.Write(new BudgetBalance { Id = "balance", Kind = "balance", ReservedUsd = 1.23456789m, LimitUsd = 50 });
        Assert.True(await tx.CommitAsync());
        batch.Received(1).ReplaceItem("config", Arg.Is<JsonElement>(json => json.GetProperty("revision").GetInt32() == 42),
            Arg.Is<TransactionalBatchItemRequestOptions>(options => options != null && options.IfMatchEtag == "config-etag"));
        batch.Received(1).CreateItem(Arg.Is<JsonElement>(json =>
            json.GetProperty("reservedUsd").GetDecimal() == 1.23456789m &&
            json.GetProperty("partitionKey").GetString() == "budget:tenant"), Arg.Any<TransactionalBatchItemRequestOptions>());
        await batch.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.PreconditionFailed)]
    public async Task Conflict_RequiresWholeTransactionRetry(HttpStatusCode status)
    {
        var container = Substitute.For<Container>();
        var batch = Substitute.For<TransactionalBatch>();
        container.CreateTransactionalBatch(Arg.Any<PartitionKey>()).Returns(batch);
        var response = Substitute.For<TransactionalBatchResponse>();
        response.StatusCode.Returns(status);
        response.IsSuccessStatusCode.Returns(false);
        batch.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(response);
        var read = Substitute.For<ItemResponse<BudgetBalance>>();
        read.Resource.Returns(new BudgetBalance { Id = "balance" });
        read.ETag.Returns("old-etag");
        container.ReadItemAsync<BudgetBalance>("balance", Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>()).Returns(read);
        var tx = new CosmosBudgetStore.Transaction(container, "budget:tenant", default);
        var balance = await tx.ReadAsync<BudgetBalance>("balance");
        balance!.ReservedUsd = 2;
        tx.Write(balance);
        Assert.False(await tx.CommitAsync());
        batch.Received(1).ReplaceItem("balance", Arg.Any<JsonElement>(), Arg.Is<TransactionalBatchItemRequestOptions>(o => o != null && o.IfMatchEtag == "old-etag"));
    }

    [Fact]
    public async Task StorageFailure_CannotReturnSuccessfulAuthorization()
    {
        var container = Substitute.For<Container>();
        var batch = Substitute.For<TransactionalBatch>();
        container.CreateTransactionalBatch(Arg.Any<PartitionKey>()).Returns(batch);
        var response = Substitute.For<TransactionalBatchResponse>();
        response.StatusCode.Returns(HttpStatusCode.ServiceUnavailable);
        response.IsSuccessStatusCode.Returns(false);
        batch.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(response);
        var read = Substitute.For<ItemResponse<BudgetBalance>>();
        read.Resource.Returns(new BudgetBalance { Id = "balance" });
        read.ETag.Returns("etag");
        container.ReadItemAsync<BudgetBalance>("balance", Arg.Any<PartitionKey>(), Arg.Any<ItemRequestOptions>(), Arg.Any<CancellationToken>()).Returns(read);
        var tx = new CosmosBudgetStore.Transaction(container, "budget:tenant", default);
        tx.Write((await tx.ReadAsync<BudgetBalance>("balance"))!);
        var error = await Assert.ThrowsAsync<BudgetException>(() => tx.CommitAsync());
        Assert.Equal(503, error.Status);
    }
}
