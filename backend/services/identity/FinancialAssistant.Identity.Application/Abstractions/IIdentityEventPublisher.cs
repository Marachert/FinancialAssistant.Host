using FinancialAssistant.Identity.Application.Messaging;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IIdentityEventPublisher
{
    Task PublishAsync(IdentityEventPublication publication, CancellationToken cancellationToken = default);
}
