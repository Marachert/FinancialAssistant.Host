using System.Text.Json;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.FinancialScore.Infrastructure;
using FinancialAssistant.Shared.Contracts.Events;
using Xunit;

namespace FinancialAssistant.FinancialScore.Tests;

public sealed class FinancialScoreMessageHandlerTests
{
    [Fact]
    public async Task Handler_ConsumesConfirmedFinancialEnvelope()
    {
        var publisher = new InMemoryFinancialScoreEventPublisher();
        var service = new FinancialScoreService(
            new InMemoryFinancialScoreStore(),
            publisher,
            new FinancialScoreCalculator());
        var handler = new FinancialScoreFinancialEventMessageHandler(service);
        var source = FinancialScoreServiceTests.CreateEvent(
            "handler-expense",
            0,
            FinancialRecordEventTypes.ExpenseCreated,
            25m);

        await handler.HandleAsync(
            JsonSerializer.SerializeToUtf8Bytes(source, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CancellationToken.None);

        Assert.Single(publisher.Published);
        Assert.Equal(
            FinancialScoreEventTypes.ScoreCalculated,
            publisher.Published.Single().EventType);
    }

    [Fact]
    public async Task Handler_RejectsInvalidJson()
    {
        var handler = new FinancialScoreFinancialEventMessageHandler(
            new FinancialScoreService(
                new InMemoryFinancialScoreStore(),
                new InMemoryFinancialScoreEventPublisher(),
                new FinancialScoreCalculator()));

        await Assert.ThrowsAsync<JsonException>(() =>
            handler.HandleAsync("not-json"u8.ToArray(), CancellationToken.None));
    }
}
