using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Messaging;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Identity.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Tests;

public sealed class IdentityEventPublishingTests
{
    [Fact]
    public async Task Publisher_CreatesSharedPrivacySafeEnvelopeInOutbox()
    {
        var now = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var options = CreateOptions();
        var outbox = new InMemoryIdentityEventOutbox();
        var publisher = new OutboxIdentityEventPublisher(
            outbox,
            new HmacIdentityEventSubjectHasher(options),
            options);

        await publisher.PublishAsync(
            new IdentityEventPublication(
                "user.signed_in.v1",
                1,
                now,
                "corr-synthetic-001",
                "request-synthetic-001",
                "synthetic-user-id",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sessionId"] = "synthetic-session-id",
                    ["authenticationMethod"] = "email_password"
                }));

        var pending = await outbox.ReadPendingAsync(10, now);
        var envelope = Assert.Single(pending).Envelope;
        Assert.True(Guid.TryParse(envelope.EventId, out _));
        Assert.Equal("user.signed_in.v1", envelope.EventType);
        Assert.Equal(1, envelope.SchemaVersion);
        Assert.Equal("financial-assistant-identity-service", envelope.Producer);
        Assert.Equal("corr-synthetic-001", envelope.CorrelationId);
        Assert.Equal("request-synthetic-001", envelope.CausationId);
        Assert.NotNull(envelope.UserIdHash);
        Assert.NotEqual("synthetic-user-id", envelope.UserIdHash);
        Assert.DoesNotContain("email", envelope.Payload.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", envelope.Payload.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Null(envelope.PublishedAt);
    }

    [Fact]
    public async Task Dispatcher_RetriesFailedTransportAndMarksConfirmedPublish()
    {
        var clock = new MutableClock(
            new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero));
        var options = CreateOptions();
        var outbox = new InMemoryIdentityEventOutbox();
        var transport = new FailOnceTransport();
        var dispatcher = new IdentityOutboxDispatcher(
            outbox,
            transport,
            clock,
            options,
            NullLogger<IdentityOutboxDispatcher>.Instance);
        var envelope = new IdentityIntegrationEventEnvelope(
            Guid.NewGuid().ToString("D"),
            "token.revoked.v1",
            clock.UtcNow,
            null,
            "financial-assistant-identity-service",
            1,
            "corr-synthetic-002",
            "request-synthetic-002",
            "safe-user-hash",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sessionId"] = "synthetic-session-id",
                ["reason"] = "logout"
            });
        await outbox.EnqueueAsync(envelope);

        Assert.Equal(0, await dispatcher.DispatchPendingAsync());
        Assert.Equal(1, transport.AttemptCount);
        Assert.Empty(await outbox.ReadPendingAsync(10, clock.UtcNow));

        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, await dispatcher.DispatchPendingAsync());
        Assert.Equal(2, transport.AttemptCount);
        Assert.NotNull(transport.Published);
        Assert.NotNull(transport.Published.PublishedAt);
        Assert.Empty(await outbox.ReadPendingAsync(10, clock.UtcNow));
    }

    private static IOptions<IdentityServiceOptions> CreateOptions()
    {
        return Options.Create(new IdentityServiceOptions
        {
            ServiceName = "financial-assistant-identity-service",
            Events = new IdentityEventPublishingOptions
            {
                UserIdHmacKey = "synthetic-event-hmac-key-for-tests-only",
                BatchSize = 10,
                MaximumRetryDelaySeconds = 60
            }
        });
    }

    private sealed class MutableClock : ISystemClock
    {
        public MutableClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan duration)
        {
            UtcNow = UtcNow.Add(duration);
        }
    }

    private sealed class FailOnceTransport : IIdentityEventTransport
    {
        public int AttemptCount { get; private set; }
        public IdentityIntegrationEventEnvelope? Published { get; private set; }

        public Task PublishAsync(
            IdentityIntegrationEventEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AttemptCount++;
            if (AttemptCount == 1)
            {
                throw new InvalidOperationException("Synthetic transient transport failure.");
            }

            Published = envelope;
            return Task.CompletedTask;
        }
    }
}
