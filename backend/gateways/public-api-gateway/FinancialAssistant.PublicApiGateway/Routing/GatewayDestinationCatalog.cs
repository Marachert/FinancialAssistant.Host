using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Routing;

public sealed class GatewayDestinationCatalog
{
    private readonly IReadOnlyDictionary<string, GatewayDestinationDefinition> destinations;
    private readonly IReadOnlyDictionary<string, Uri> baseAddresses;

    public GatewayDestinationCatalog(IOptions<GatewayDestinationMapOptions> options)
    {
        var configuredDestinations = options.Value.Destinations ?? Array.Empty<GatewayDestinationDefinition>();
        Validate(configuredDestinations);

        destinations = configuredDestinations.ToDictionary(
            destination => destination.DestinationKey,
            destination => destination,
            StringComparer.OrdinalIgnoreCase);
        baseAddresses = configuredDestinations
            .Where(destination => !string.IsNullOrWhiteSpace(destination.BaseAddress))
            .ToDictionary(
                destination => destination.DestinationKey,
                destination => new Uri(destination.BaseAddress, UriKind.Absolute),
                StringComparer.OrdinalIgnoreCase);

        Destinations = configuredDestinations
            .OrderBy(destination => destination.DestinationKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyCollection<GatewayDestinationDefinition> Destinations { get; }

    public bool TryGetDestination(string destinationKey, out GatewayDestinationDefinition destination)
    {
        return destinations.TryGetValue(destinationKey, out destination!);
    }

    public bool TryGetBaseAddress(string destinationKey, out Uri baseAddress)
    {
        return baseAddresses.TryGetValue(destinationKey, out baseAddress!);
    }

    private static void Validate(IReadOnlyList<GatewayDestinationDefinition> configuredDestinations)
    {
        if (configuredDestinations.Count == 0)
        {
            throw new InvalidOperationException("Gateway destination configuration must contain at least one destination.");
        }

        var destinationKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var destination in configuredDestinations)
        {
            if (string.IsNullOrWhiteSpace(destination.DestinationKey)
                || destination.DestinationKey.Any(character =>
                    !(char.IsLower(character) || char.IsDigit(character) || character == '-')))
            {
                throw new InvalidOperationException("Gateway destination keys must use lower-case kebab-case characters only.");
            }

            if (!destinationKeys.Add(destination.DestinationKey))
            {
                throw new InvalidOperationException($"Duplicate gateway destination key '{destination.DestinationKey}'.");
            }

            if (destination.RequestTimeoutSeconds is < 1 or > 300)
            {
                throw new InvalidOperationException(
                    $"Gateway destination '{destination.DestinationKey}' must use a timeout between 1 and 300 seconds.");
            }

            if (string.IsNullOrWhiteSpace(destination.BaseAddress))
            {
                if (destination.Enabled)
                {
                    throw new InvalidOperationException(
                        $"Enabled gateway destination '{destination.DestinationKey}' requires a base address.");
                }

                continue;
            }

            if (!Uri.TryCreate(destination.BaseAddress, UriKind.Absolute, out var address)
                || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrWhiteSpace(address.UserInfo)
                || !string.IsNullOrWhiteSpace(address.Query)
                || !string.IsNullOrWhiteSpace(address.Fragment))
            {
                throw new InvalidOperationException(
                    $"Gateway destination '{destination.DestinationKey}' has an invalid base address.");
            }
        }
    }
}
