using FinancialAssistant.Category.Contracts;

namespace FinancialAssistant.Category.Application.Abstractions;

public interface ICategoryEventPublisher
{
    Task PublishAsync(CategoryUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
