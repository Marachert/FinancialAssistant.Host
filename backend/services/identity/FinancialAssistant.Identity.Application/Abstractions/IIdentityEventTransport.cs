using FinancialAssistant.Identity.Application.Messaging;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IIdentityEventTransport
{
    Task PublishAsync(
        IdentityIntegrationEventEnvelope envelope,
        CancellationToken cancellationToken = default);
}
