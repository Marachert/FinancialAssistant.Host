using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Tests;

public sealed class IdentityContractShapeTests
{
    private static readonly string[] ForbiddenStorageTerms =
    [
        "Hash",
        "PhysicalIndex",
        "ReadAlias",
        "WriteAlias",
        "SchemaVersion",
        "PrimaryTerm",
        "SequenceNumber",
        "DeletedAtUtc"
    ];

    [Fact]
    public void ClientContracts_DoNotExposeStorageImplementationFields()
    {
        var contractTypes = typeof(RegisterAccountRequest)
            .Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(RegisterAccountRequest).Namespace)
            .Where(type => type.IsClass && !type.IsAbstract)
            .ToArray();

        foreach (var contractType in contractTypes)
        {
            foreach (var property in contractType.GetProperties())
            {
                Assert.DoesNotContain(
                    ForbiddenStorageTerms,
                    term => property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void ErrorCodes_AreStableAndUnique()
    {
        Assert.Equal(IdentityErrorCodes.All.Count, IdentityErrorCodes.All.Distinct().Count());
        Assert.All(
            IdentityErrorCodes.All,
            code => Assert.Matches("^[a-z]+(?:_[a-z]+)*$", code));
    }

    [Fact]
    public void VersionOneRoutes_AreUniqueAndRemainUnderAuthBoundary()
    {
        var routes = new[]
        {
            IdentityApiRoutes.Register,
            IdentityApiRoutes.SignIn,
            IdentityApiRoutes.Refresh,
            IdentityApiRoutes.Logout,
            IdentityApiRoutes.CurrentUser
        };

        Assert.Equal(routes.Length, routes.Distinct().Count());
        Assert.All(routes, route => Assert.StartsWith("/auth/v1/", route, StringComparison.Ordinal));
    }
}
