using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Api.Endpoints;

public static class IdentityContractEndpointExtensions
{
    public static IEndpointRouteBuilder MapIdentityContractEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(IdentityApiRoutes.Base).WithTags("Identity v1");
        group.MapIdentityCredentialEndpoints();
        group.MapIdentityProviderEndpoints();
        group.MapIdentitySessionLifecycleEndpoints();
        return endpoints;
    }
}
