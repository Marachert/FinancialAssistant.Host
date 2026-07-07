using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Routing;

public sealed class GatewayDestinationCatalog
{
    private readonly IReadOnlyDictionary<string, GatewayDestinationDefinition> destinations;

    public GatewayDestinationCatalog(IOptions<GatewayDestinationMapOptions> options)
    {
        destinations = options.Value.Destinations
            .Where(destination => !string.IsNullOrWhiteSpace(destination.DestinationKey))
            .GroupBy(destination => destination.DestinationKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        Destinations = destinations.Values.ToArray();
    }

    public IReadOnlyCollection<GatewayDestinationDefinition> Destinations { get; }

    public bool TryGetDestination(string destinationKey, out GatewayDestinationDefinition destination)
    {
        return destinations.TryGetValue(destinationKey, out destination!);
    }
}
