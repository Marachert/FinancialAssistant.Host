namespace FinancialAssistant.Identity.Application.Abstractions;

public interface ISessionLifetimePolicy
{
    TimeSpan AccessLifetime { get; }
    TimeSpan RenewalLifetime { get; }
}
