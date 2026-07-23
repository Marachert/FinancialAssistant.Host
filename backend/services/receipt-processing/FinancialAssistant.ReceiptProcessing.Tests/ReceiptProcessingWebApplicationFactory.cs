using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Events;
using FinancialAssistant.TransactionIntake.Application;
using FinancialAssistant.TransactionIntake.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.ReceiptProcessing.Tests;

public sealed class ReceiptProcessingWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string GatewaySecret = "synthetic-receipt-gateway-key-2026";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ReceiptProcessing:Gateway:SharedSecret"] = GatewaySecret
                }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOcrProvider>();
            services.AddSingleton<IOcrProvider, SyntheticOcrProvider>();
            services.RemoveAll<IOcrCompletedPublisher>();
            services.AddSingleton<IOcrCompletedPublisher>(provider =>
                provider.GetRequiredService<InMemoryOcrCompletedPublisher>());
            services.AddTransactionIntakeApplication();
            services.AddTransactionIntakeInfrastructure();
        });
    }

    private sealed class SyntheticOcrProvider : IOcrProvider
    {
        public Task<OcrExtractionResult> ExtractAsync(
            Stream receiptImage,
            string contentType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                new OcrExtractionResult(
                    "123.45 USD 2026-07-23 merchant: Synthetic Market",
                    0.91m,
                    new[] { "merchant_uncertain" }));
        }
    }
}
