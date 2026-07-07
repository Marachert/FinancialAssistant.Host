namespace FinancialAssistant.PublicApiGateway.Diagnostics;

public sealed class GatewayDiagnosticsClock
{
    public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;

    public TimeSpan Uptime => DateTimeOffset.UtcNow - StartedAtUtc;
}
