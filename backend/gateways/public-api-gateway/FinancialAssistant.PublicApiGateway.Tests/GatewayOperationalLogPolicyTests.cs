using System.Reflection;
using System.Text.RegularExpressions;
using FinancialAssistant.PublicApiGateway.Observability;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class GatewayOperationalLogPolicyTests
{
    private static readonly HashSet<string> AllowedFields = new(StringComparer.Ordinal)
    {
        "StatusCode",
        "ElapsedMilliseconds",
        "RouteKey",
        "DestinationKey",
        "TimeoutSeconds",
        "FailureType",
        "ResponseStarted",
        "AccessPolicy",
        "AuthenticationResult"
    };

    private static readonly string[] ForbiddenFragments =
    {
        "Authorization",
        "Bearer",
        "Token",
        "Password",
        "Secret",
        "ApiKey",
        "Cookie",
        "Body",
        "Payload",
        "Query",
        "Email",
        "Phone",
        "UserId",
        "SessionId",
        "Roles",
        "Receipt",
        "Ocr",
        "Llm",
        "Exception",
        "StackTrace"
    };

    [Fact]
    public void OperationalEvents_UseStableSafeStructuredContract()
    {
        var events = typeof(GatewayOperationalLog)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetCustomAttribute<LoggerMessageAttribute>()
            })
            .Where(item => item.Attribute is not null)
            .ToArray();

        Assert.NotEmpty(events);
        Assert.Equal(events.Length, events.Select(item => item.Attribute!.EventId).Distinct().Count());
        Assert.Equal(
            events.Length,
            events.Select(item => item.Attribute!.EventName).Distinct(StringComparer.Ordinal).Count());

        foreach (var item in events)
        {
            var attribute = item.Attribute!;
            Assert.InRange(attribute.EventId, 1000, 1999);
            Assert.Matches("^Gateway[A-Z][A-Za-z0-9]+$", attribute.EventName ?? string.Empty);
            Assert.False(string.IsNullOrWhiteSpace(attribute.Message));
            Assert.DoesNotContain(
                item.Method.GetParameters(),
                parameter => typeof(Exception).IsAssignableFrom(parameter.ParameterType));

            foreach (var forbiddenFragment in ForbiddenFragments)
            {
                Assert.DoesNotContain(
                    forbiddenFragment,
                    attribute.Message!,
                    StringComparison.OrdinalIgnoreCase);
            }

            var placeholders = Regex.Matches(
                    attribute.Message!,
                    @"\{(?<name>[A-Za-z][A-Za-z0-9]*)")
                .Select(match => match.Groups["name"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Assert.All(placeholders, field => Assert.Contains(field, AllowedFields));

            var structuredParameters = item.Method
                .GetParameters()
                .Where(parameter => parameter.ParameterType != typeof(ILogger))
                .Select(parameter => ToPascalCase(parameter.Name!))
                .ToArray();

            Assert.All(structuredParameters, field => Assert.Contains(field, AllowedFields));
        }
    }

    private static string ToPascalCase(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
}
