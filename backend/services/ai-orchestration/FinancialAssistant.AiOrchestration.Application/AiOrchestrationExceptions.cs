namespace FinancialAssistant.AiOrchestration.Application;

public sealed class AiCapabilityNotConfiguredException : InvalidOperationException
{
    public AiCapabilityNotConfiguredException(string capabilityName)
        : base($"AI capability '{capabilityName}' is not configured.")
    {
    }
}

public sealed class LlmProviderNotConfiguredException : InvalidOperationException
{
    public LlmProviderNotConfiguredException(string providerName)
        : base($"LLM provider '{providerName}' is not configured.")
    {
    }
}

public sealed class LlmProviderException : InvalidOperationException
{
    public LlmProviderException(
        string providerName,
        string code,
        bool isTransient)
        : base($"LLM provider '{providerName}' could not complete the request.")
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Value is required.", nameof(providerName));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Value is required.", nameof(code));
        }

        ProviderName = providerName;
        Code = code;
        IsTransient = isTransient;
    }

    public string ProviderName { get; }

    public string Code { get; }

    public bool IsTransient { get; }
}

public sealed class PromptNotFoundException : InvalidOperationException
{
    public PromptNotFoundException(string promptName, int? version)
        : base(version is null
            ? $"Prompt '{promptName}' is not registered."
            : $"Prompt '{promptName}' version {version.Value} is not registered.")
    {
    }
}

public sealed class StructuredOutputValidationException : InvalidOperationException
{
    public StructuredOutputValidationException(IReadOnlyList<string> errors)
        : base("The LLM provider returned output that does not satisfy the registered JSON schema.")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}

public sealed class InvalidJsonSchemaException : InvalidOperationException
{
    public InvalidJsonSchemaException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
