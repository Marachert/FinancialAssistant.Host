namespace FinancialAssistant.AiOrchestration.Domain;

public sealed record PromptDefinition
{
    public PromptDefinition(
        string name,
        int version,
        string template,
        string outputJsonSchema)
    {
        EnsureRequired(name, nameof(name));
        EnsureRequired(template, nameof(template));
        EnsureRequired(outputJsonSchema, nameof(outputJsonSchema));
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Prompt version must be positive.");
        }

        Name = name;
        Version = version;
        Template = template;
        OutputJsonSchema = outputJsonSchema;
    }

    public string Name { get; }

    public int Version { get; }

    public string Template { get; }

    public string OutputJsonSchema { get; }

    private static void EnsureRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }
    }
}
