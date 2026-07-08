using Microsoft.Extensions.Logging;

namespace FinancialAssistant.PublicApiGateway.Observability;

public static partial class GatewayOperationalLog
{
    [LoggerMessage(
        EventId = 1000,
        EventName = "GatewayRequestStarted",
        Level = LogLevel.Information,
        Message = "Gateway request started.")]
    public static partial void RequestStarted(ILogger logger);

    [LoggerMessage(
        EventId = 1001,
        EventName = "GatewayRequestCompleted",
        Level = LogLevel.Information,
        Message = "Gateway request completed. StatusCode: {StatusCode}; ElapsedMilliseconds: {ElapsedMilliseconds}.")]
    public static partial void RequestCompleted(
        ILogger logger,
        int statusCode,
        double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1002,
        EventName = "GatewayClientRequestCancelled",
        Level = LogLevel.Information,
        Message = "Gateway request was cancelled by the client.")]
    public static partial void ClientRequestCancelled(ILogger logger);

    [LoggerMessage(
        EventId = 1100,
        EventName = "GatewayDestinationUnavailable",
        Level = LogLevel.Warning,
        Message = "Gateway destination is unavailable. RouteKey: {RouteKey}; DestinationKey: {DestinationKey}.")]
    public static partial void DestinationUnavailable(
        ILogger logger,
        string routeKey,
        string destinationKey);

    [LoggerMessage(
        EventId = 1101,
        EventName = "GatewayDispatchCancelled",
        Level = LogLevel.Information,
        Message = "Gateway dispatch was cancelled. RouteKey: {RouteKey}.")]
    public static partial void DispatchCancelled(
        ILogger logger,
        string routeKey);

    [LoggerMessage(
        EventId = 1102,
        EventName = "GatewayDestinationTimedOut",
        Level = LogLevel.Warning,
        Message = "Gateway destination timed out. RouteKey: {RouteKey}; DestinationKey: {DestinationKey}; TimeoutSeconds: {TimeoutSeconds}.")]
    public static partial void DestinationTimedOut(
        ILogger logger,
        string routeKey,
        string destinationKey,
        int timeoutSeconds);

    [LoggerMessage(
        EventId = 1103,
        EventName = "GatewayDestinationCallFailed",
        Level = LogLevel.Warning,
        Message = "Gateway destination call failed. RouteKey: {RouteKey}; DestinationKey: {DestinationKey}; FailureType: {FailureType}.")]
    public static partial void DestinationCallFailed(
        ILogger logger,
        string routeKey,
        string destinationKey,
        string failureType);

    [LoggerMessage(
        EventId = 1200,
        EventName = "GatewaySecurityPlaceholderEvaluated",
        Level = LogLevel.Debug,
        Message = "Gateway security boundary evaluated in placeholder mode. RouteKey: {RouteKey}; AccessPolicy: {AccessPolicy}.")]
    public static partial void SecurityPlaceholderEvaluated(
        ILogger logger,
        string routeKey,
        string accessPolicy);

    [LoggerMessage(
        EventId = 1201,
        EventName = "GatewayPublicRequestAllowed",
        Level = LogLevel.Debug,
        Message = "Gateway allowed public request. RouteKey: {RouteKey}.")]
    public static partial void PublicRequestAllowed(
        ILogger logger,
        string routeKey);

    [LoggerMessage(
        EventId = 1202,
        EventName = "GatewayAuthenticationRejected",
        Level = LogLevel.Warning,
        Message = "Gateway rejected request authentication. RouteKey: {RouteKey}; AuthenticationResult: {AuthenticationResult}.")]
    public static partial void AuthenticationRejected(
        ILogger logger,
        string routeKey,
        string authenticationResult);

    [LoggerMessage(
        EventId = 1203,
        EventName = "GatewayAdminRoleRejected",
        Level = LogLevel.Warning,
        Message = "Gateway rejected admin route request. RouteKey: {RouteKey}.")]
    public static partial void AdminRoleRejected(
        ILogger logger,
        string routeKey);

    [LoggerMessage(
        EventId = 1204,
        EventName = "GatewayRequestAuthorized",
        Level = LogLevel.Debug,
        Message = "Gateway authorized request. RouteKey: {RouteKey}; AccessPolicy: {AccessPolicy}.")]
    public static partial void RequestAuthorized(
        ILogger logger,
        string routeKey,
        string accessPolicy);

    [LoggerMessage(
        EventId = 1900,
        EventName = "GatewayUnhandledFailure",
        Level = LogLevel.Error,
        Message = "Unhandled gateway failure. FailureType: {FailureType}; ResponseStarted: {ResponseStarted}.")]
    public static partial void UnhandledFailure(
        ILogger logger,
        string failureType,
        bool responseStarted);
}
