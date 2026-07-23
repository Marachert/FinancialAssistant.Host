using System.Globalization;
using System.Text.Json;
using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Application.Abstractions;

namespace FinancialAssistant.AiOrchestration.Infrastructure.Validation;

public sealed class JsonSchemaStructuredOutputValidator : IStructuredOutputValidator
{
    private static readonly HashSet<string> SupportedKeywords = new(
        new[]
        {
            "$schema",
            "$id",
            "title",
            "description",
            "default",
            "examples",
            "type",
            "enum",
            "properties",
            "required",
            "additionalProperties",
            "items",
            "minItems",
            "maxItems",
            "minLength",
            "maxLength",
            "minimum",
            "maximum",
        },
        StringComparer.Ordinal);

    public StructuredOutputValidationResult Validate(
        string structuredOutputJson,
        string jsonSchema)
    {
        JsonDocument schema;
        try
        {
            schema = JsonDocument.Parse(jsonSchema);
        }
        catch (JsonException exception)
        {
            throw new InvalidJsonSchemaException("The registered output JSON schema is not valid JSON.", exception);
        }

        using (schema)
        {
            if (schema.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidJsonSchemaException("The registered output JSON schema must be an object.");
            }

            ValidateSchemaNode(schema.RootElement, "$");

            JsonDocument output;
            try
            {
                output = JsonDocument.Parse(structuredOutputJson);
            }
            catch (JsonException)
            {
                return new StructuredOutputValidationResult(
                    false,
                    new[] { "$: output is not valid JSON." });
            }

            using (output)
            {
                var errors = new List<string>();
                ValidateNode(output.RootElement, schema.RootElement, "$", errors);
                return new StructuredOutputValidationResult(errors.Count == 0, errors);
            }
        }
    }

    private static void ValidateNode(
        JsonElement value,
        JsonElement schema,
        string path,
        List<string> errors)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidJsonSchemaException($"Schema at '{path}' must be an object.");
        }

        ValidateSupportedKeywords(schema, path);
        ValidateType(value, schema, path, errors);
        ValidateEnum(value, schema, path, errors);

        if (value.ValueKind == JsonValueKind.Object)
        {
            ValidateObject(value, schema, path, errors);
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            ValidateArray(value, schema, path, errors);
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            ValidateString(value, schema, path, errors);
        }
        else if (value.ValueKind == JsonValueKind.Number)
        {
            ValidateNumber(value, schema, path, errors);
        }
    }

    private static void ValidateType(
        JsonElement value,
        JsonElement schema,
        string path,
        List<string> errors)
    {
        if (!schema.TryGetProperty("type", out var typeNode))
        {
            return;
        }

        var allowedTypes = ReadAllowedTypes(typeNode, path);

        if (!allowedTypes.Any(type => MatchesType(value, type)))
        {
            errors.Add($"{path}: expected type {string.Join(" or ", allowedTypes)}.");
        }
    }

    private static bool MatchesType(JsonElement value, string type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "number" => value.ValueKind == JsonValueKind.Number,
        "integer" => value.ValueKind == JsonValueKind.Number &&
            value.TryGetDecimal(out var number) && number == decimal.Truncate(number),
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => throw new InvalidJsonSchemaException($"Unsupported JSON schema type '{type}'."),
    };

    private static void ValidateEnum(
        JsonElement value,
        JsonElement schema,
        string path,
        List<string> errors)
    {
        if (!schema.TryGetProperty("enum", out var enumNode))
        {
            return;
        }

        if (enumNode.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidJsonSchemaException($"Schema enum at '{path}' must be an array.");
        }

        if (!enumNode.EnumerateArray().Any(candidate => JsonEquals(candidate, value)))
        {
            errors.Add($"{path}: value is not in the allowed enum.");
        }
    }

    private static void ValidateObject(
        JsonElement value,
        JsonElement schema,
        string path,
        List<string> errors)
    {
        var properties = schema.TryGetProperty("properties", out var propertiesNode)
            ? propertiesNode
            : default;
        if (properties.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Object)
        {
            throw new InvalidJsonSchemaException($"Schema properties at '{path}' must be an object.");
        }

        if (schema.TryGetProperty("required", out var requiredNode))
        {
            if (requiredNode.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidJsonSchemaException($"Schema required at '{path}' must be an array.");
            }

            foreach (var required in requiredNode.EnumerateArray())
            {
                if (required.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidJsonSchemaException($"Schema required at '{path}' must contain strings.");
                }

                var propertyName = required.GetString()!;
                if (!value.TryGetProperty(propertyName, out _))
                {
                    errors.Add($"{path}.{propertyName}: required property is missing.");
                }
            }
        }

        var rejectAdditional = false;
        if (schema.TryGetProperty("additionalProperties", out var additional))
        {
            if (additional.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                throw new InvalidJsonSchemaException(
                    $"Schema additionalProperties at '{path}' must be a boolean.");
            }

            rejectAdditional = additional.ValueKind == JsonValueKind.False;
        }

        foreach (var property in value.EnumerateObject())
        {
            if (properties.ValueKind == JsonValueKind.Object &&
                properties.TryGetProperty(property.Name, out var propertySchema))
            {
                ValidateNode(property.Value, propertySchema, $"{path}.{property.Name}", errors);
            }
            else if (rejectAdditional)
            {
                errors.Add($"{path}.{property.Name}: additional property is not allowed.");
            }
        }
    }

    private static void ValidateArray(
        JsonElement value,
        JsonElement schema,
        string path,
        List<string> errors)
    {
        if (schema.TryGetProperty("minItems", out var minItems) &&
            value.GetArrayLength() < ReadNonNegativeInteger(minItems, "minItems", path))
        {
            errors.Add($"{path}: array has fewer items than allowed.");
        }

        if (schema.TryGetProperty("maxItems", out var maxItems) &&
            value.GetArrayLength() > ReadNonNegativeInteger(maxItems, "maxItems", path))
        {
            errors.Add($"{path}: array has more items than allowed.");
        }

        if (!schema.TryGetProperty("items", out var items))
        {
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            ValidateNode(item, items, $"{path}[{index}]", errors);
            index++;
        }
    }

    private static void ValidateString(
        JsonElement value,
        JsonElement schema,
        string path,
        List<string> errors)
    {
        var length = value.GetString()!.Length;
        if (schema.TryGetProperty("minLength", out var minLength) &&
            length < ReadNonNegativeInteger(minLength, "minLength", path))
        {
            errors.Add($"{path}: string is shorter than allowed.");
        }

        if (schema.TryGetProperty("maxLength", out var maxLength) &&
            length > ReadNonNegativeInteger(maxLength, "maxLength", path))
        {
            errors.Add($"{path}: string is longer than allowed.");
        }
    }

    private static void ValidateNumber(
        JsonElement value,
        JsonElement schema,
        string path,
        List<string> errors)
    {
        var number = value.GetDecimal();
        if (schema.TryGetProperty("minimum", out var minimum) &&
            number < ReadDecimal(minimum, "minimum", path))
        {
            errors.Add($"{path}: number is below the minimum.");
        }

        if (schema.TryGetProperty("maximum", out var maximum) &&
            number > ReadDecimal(maximum, "maximum", path))
        {
            errors.Add($"{path}: number is above the maximum.");
        }
    }

    private static int ReadNonNegativeInteger(JsonElement value, string keyword, string path)
    {
        if (!value.TryGetInt32(out var result) || result < 0)
        {
            throw new InvalidJsonSchemaException(
                $"Schema {keyword} at '{path}' must be a non-negative integer.");
        }

        return result;
    }

    private static decimal ReadDecimal(JsonElement value, string keyword, string path)
    {
        if (!value.TryGetDecimal(out var result))
        {
            throw new InvalidJsonSchemaException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Schema {keyword} at '{path}' must be a number."));
        }

        return result;
    }

    private static bool JsonEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number &&
                right.ValueKind == JsonValueKind.Number &&
                left.TryGetDecimal(out var leftNumber) &&
                right.TryGetDecimal(out var rightNumber) &&
                leftNumber == rightNumber;
        }

        return left.ValueKind switch
        {
            JsonValueKind.Object => ObjectEquals(left, right),
            JsonValueKind.Array => left.EnumerateArray()
                .Zip(right.EnumerateArray())
                .All(pair => JsonEquals(pair.First, pair.Second)) &&
                left.GetArrayLength() == right.GetArrayLength(),
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.TryGetDecimal(out var leftNumber) &&
                right.TryGetDecimal(out var rightNumber) &&
                leftNumber == rightNumber,
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => left.GetRawText() == right.GetRawText(),
        };
    }

    private static bool ObjectEquals(JsonElement left, JsonElement right)
    {
        var leftProperties = left.EnumerateObject().ToArray();
        var rightProperties = right.EnumerateObject().ToArray();
        return leftProperties.Length == rightProperties.Length &&
            leftProperties.All(property =>
                right.TryGetProperty(property.Name, out var rightValue) &&
                JsonEquals(property.Value, rightValue));
    }

    private static void ValidateSupportedKeywords(JsonElement schema, string path)
    {
        foreach (var keyword in schema.EnumerateObject())
        {
            if (!SupportedKeywords.Contains(keyword.Name))
            {
                throw new InvalidJsonSchemaException(
                    $"JSON schema keyword '{keyword.Name}' at '{path}' is not supported.");
            }
        }
    }

    private static void ValidateSchemaNode(JsonElement schema, string path)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidJsonSchemaException($"Schema at '{path}' must be an object.");
        }

        ValidateSupportedKeywords(schema, path);

        if (schema.TryGetProperty("type", out var typeNode))
        {
            _ = ReadAllowedTypes(typeNode, path);
        }

        if (schema.TryGetProperty("enum", out var enumNode) &&
            (enumNode.ValueKind != JsonValueKind.Array || enumNode.GetArrayLength() == 0))
        {
            throw new InvalidJsonSchemaException(
                $"Schema enum at '{path}' must be a non-empty array.");
        }

        if (schema.TryGetProperty("properties", out var propertiesNode))
        {
            if (propertiesNode.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidJsonSchemaException(
                    $"Schema properties at '{path}' must be an object.");
            }

            foreach (var property in propertiesNode.EnumerateObject())
            {
                ValidateSchemaNode(property.Value, $"{path}.properties.{property.Name}");
            }
        }

        if (schema.TryGetProperty("required", out var requiredNode))
        {
            if (requiredNode.ValueKind != JsonValueKind.Array ||
                requiredNode.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
            {
                throw new InvalidJsonSchemaException(
                    $"Schema required at '{path}' must be an array of strings.");
            }
        }

        if (schema.TryGetProperty("additionalProperties", out var additionalProperties) &&
            additionalProperties.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new InvalidJsonSchemaException(
                $"Schema additionalProperties at '{path}' must be a boolean.");
        }

        if (schema.TryGetProperty("items", out var itemsNode))
        {
            ValidateSchemaNode(itemsNode, $"{path}.items");
        }

        ValidateNonNegativeIntegerKeyword(schema, "minItems", path);
        ValidateNonNegativeIntegerKeyword(schema, "maxItems", path);
        ValidateNonNegativeIntegerKeyword(schema, "minLength", path);
        ValidateNonNegativeIntegerKeyword(schema, "maxLength", path);
        ValidateNumberKeyword(schema, "minimum", path);
        ValidateNumberKeyword(schema, "maximum", path);
        ValidateStringKeyword(schema, "$schema", path);
        ValidateStringKeyword(schema, "$id", path);
        ValidateStringKeyword(schema, "title", path);
        ValidateStringKeyword(schema, "description", path);

        if (schema.TryGetProperty("examples", out var examplesNode) &&
            examplesNode.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidJsonSchemaException(
                $"Schema examples at '{path}' must be an array.");
        }
    }

    private static string[] ReadAllowedTypes(JsonElement typeNode, string path)
    {
        var allowedTypes = typeNode.ValueKind switch
        {
            JsonValueKind.String => new[] { typeNode.GetString()! },
            JsonValueKind.Array => typeNode.EnumerateArray()
                .Select(type => type.ValueKind == JsonValueKind.String
                    ? type.GetString()!
                    : throw new InvalidJsonSchemaException(
                        $"Schema type at '{path}' must contain strings."))
                .ToArray(),
            _ => throw new InvalidJsonSchemaException(
                $"Schema type at '{path}' must be a string or array."),
        };

        if (allowedTypes.Length == 0)
        {
            throw new InvalidJsonSchemaException(
                $"Schema type at '{path}' must not be an empty array.");
        }

        foreach (var type in allowedTypes)
        {
            _ = type switch
            {
                "object" or "array" or "string" or "number" or "integer" or "boolean" or "null" => true,
                _ => throw new InvalidJsonSchemaException(
                    $"Unsupported JSON schema type '{type}' at '{path}'."),
            };
        }

        return allowedTypes;
    }

    private static void ValidateNonNegativeIntegerKeyword(
        JsonElement schema,
        string keyword,
        string path)
    {
        if (schema.TryGetProperty(keyword, out var value))
        {
            _ = ReadNonNegativeInteger(value, keyword, path);
        }
    }

    private static void ValidateNumberKeyword(
        JsonElement schema,
        string keyword,
        string path)
    {
        if (schema.TryGetProperty(keyword, out var value))
        {
            _ = ReadDecimal(value, keyword, path);
        }
    }

    private static void ValidateStringKeyword(
        JsonElement schema,
        string keyword,
        string path)
    {
        if (schema.TryGetProperty(keyword, out var value) &&
            value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidJsonSchemaException(
                $"Schema {keyword} at '{path}' must be a string.");
        }
    }
}
