using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Infrastructure.Validation;

namespace FinancialAssistant.AiOrchestration.Tests;

public sealed class JsonSchemaStructuredOutputValidatorTests
{
    private const string Schema = """
        {
          "type": "object",
          "required": ["kind", "amount", "tags"],
          "additionalProperties": false,
          "properties": {
            "kind": { "type": "string", "enum": ["income", "expense"] },
            "amount": { "type": "number", "minimum": 0 },
            "tags": {
              "type": "array",
              "maxItems": 2,
              "items": { "type": "string", "minLength": 1 }
            }
          }
        }
        """;

    private readonly JsonSchemaStructuredOutputValidator validator = new();

    [Fact]
    public void Validate_WhenOutputMatchesSchema_ReturnsValid()
    {
        var result = validator.Validate(
            """{"kind":"expense","amount":12.50,"tags":["food"]}""",
            Schema);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("{}", "required property is missing")]
    [InlineData("{\"kind\":\"transfer\",\"amount\":1,\"tags\":[]}", "allowed enum")]
    [InlineData("{\"kind\":\"income\",\"amount\":-1,\"tags\":[]}", "below the minimum")]
    [InlineData("{\"kind\":\"income\",\"amount\":1,\"tags\":[],\"raw\":\"placeholder\"}", "additional property")]
    [InlineData("not-json", "not valid JSON")]
    public void Validate_WhenOutputViolatesSchema_ReturnsErrors(string output, string expectedError)
    {
        var result = validator.Validate(output, Schema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WhenRegisteredSchemaIsMalformed_Throws()
    {
        Assert.Throws<InvalidJsonSchemaException>(() => validator.Validate("{}", "not-json"));
    }

    [Fact]
    public void Validate_EnumObjectComparison_IgnoresPropertyOrder()
    {
        const string schema = """
            {
              "enum": [
                { "type": "expense", "amount": 10 }
              ]
            }
            """;

        var result = validator.Validate("""{"amount":10.0,"type":"expense"}""", schema);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenSchemaUsesUnsupportedKeyword_FailsClosed()
    {
        const string schema = """{ "type": "string", "pattern": "^[a-z]+$" }""";

        Assert.Throws<InvalidJsonSchemaException>(() => validator.Validate("\"value\"", schema));
    }

    [Theory]
    [InlineData(
        """{"type":"object","properties":{"optional":{"type":"string","pattern":"^[a-z]+$"}}}""",
        "{}")]
    [InlineData(
        """{"type":"array","items":{"type":42}}""",
        "[]")]
    public void Validate_WhenUnvisitedSchemaBranchIsInvalid_FailsClosed(
        string schema,
        string output)
    {
        Assert.Throws<InvalidJsonSchemaException>(() => validator.Validate(output, schema));
    }
}
