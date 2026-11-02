namespace FinancialAssistant.Shared.Messaging;

public interface INativePublisher
{
    Task PublishAsync(NativePublication publication, CancellationToken cancellationToken = default);
}

public sealed class NativeBrokerUnavailableException() : Exception("Native message broker is unavailable.");
