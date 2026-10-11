using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FinancialAssistant.Shared.Persistence;

public static class OwnedStorageRequestBoundary
{
    public static IApplicationBuilder UseOwnedStorageFailureBoundary(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        try { await next(context); }
        catch (Exception exception) when (!context.Response.HasStarted && (exception is StorageUnavailableException or StorageConflictException
            || exception is OperationCanceledException && !context.RequestAborted.IsCancellationRequested))
        {
            await Results.Problem(title: "Service operation unavailable.", detail: "Please retry the operation later.",
                statusCode: StatusCodes.Status503ServiceUnavailable).ExecuteAsync(context);
        }
    });
}
