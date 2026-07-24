using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Domain;
using FinancialAssistant.ReceiptProcessing.Infrastructure;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Events;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.ReceiptProcessing.Tests;

public sealed class ReceiptProcessingArchitectureTests
{
    [Fact]
    public void Domain_DoesNotReferenceApiInfrastructureOrProviderClients()
    {
        var references = typeof(ReceiptProcessingDomainAssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain("FinancialAssistant.ReceiptProcessing.Api", references);
        Assert.DoesNotContain("FinancialAssistant.ReceiptProcessing.Infrastructure", references);
        Assert.DoesNotContain(references, name =>
            name!.Contains("Ocr", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EventsAndMetadata_ExcludeRawReceiptAndOcrContent()
    {
        var contractTypes = new[]
        {
            typeof(ReceiptUploadedIntegrationEvent),
            typeof(OcrCompletedIntegrationEvent),
            typeof(ReceiptFileMetadata),
            typeof(ReceiptOcrMetadata),
            typeof(OcrProcessingAuditMetadata),
            typeof(NormalizedReceiptCandidate),
            typeof(ReceiptLineItemCandidate)
        };
        var forbidden = new[]
        {
            "Raw",
            "ExtractedText",
            "ImageContent",
            "FileName",
            "ObjectKey",
            "ProviderError"
        };

        foreach (var type in contractTypes)
        {
            var properties = type.GetProperties().Select(property => property.Name).ToArray();
            Assert.DoesNotContain(
                properties,
                property => forbidden.Any(value =>
                    property.Contains(value, StringComparison.OrdinalIgnoreCase)));
        }

        var auditProperties = typeof(OcrProcessingAuditMetadata)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.Contains(nameof(OcrProcessingAuditMetadata.FailureCategory), auditProperties);
        Assert.Contains(nameof(OcrProcessingAuditMetadata.DurationMilliseconds), auditProperties);
        Assert.Contains(nameof(OcrProcessingAuditMetadata.TraceId), auditProperties);
    }

    [Fact]
    public void RuntimeObjectStorage_IsExplicitlyEncryptedAndInMemory()
    {
        Assert.Contains(
            "EncryptedInMemory",
            typeof(EncryptedInMemoryReceiptObjectStore).Name,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EventNames_AreStableAndVersioned()
    {
        Assert.Equal("receipt.uploaded.v1", ReceiptUploadedIntegrationEvent.Name);
        Assert.Equal("ocr.completed.v1", OcrCompletedIntegrationEvent.Name);
    }

    [Fact]
    public void DefaultInfrastructure_UsesInterserviceOcrCompletionDelivery()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [HttpOcrCompletedPublisher.BaseAddressConfigurationKey] =
                        "http://transaction-intake.internal",
                    [HttpOcrCompletedPublisher.SharedSecretConfigurationKey] =
                        "synthetic-interservice-secret-value"
                })
            .Build();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IConfiguration>(configuration);
        serviceCollection.AddLogging();
        serviceCollection.AddReceiptProcessingInfrastructure();
        using var provider = serviceCollection.BuildServiceProvider();

        Assert.IsType<HttpOcrCompletedPublisher>(
            provider.GetRequiredService<
                FinancialAssistant.ReceiptProcessing.Application.Abstractions.IOcrCompletedPublisher>());
    }

    [Fact]
    public void DefaultInfrastructure_DecoratesTheProviderClientWithBoundedResilience()
    {
        var configuration = new ConfigurationBuilder().Build();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IConfiguration>(configuration);
        serviceCollection.AddLogging();
        serviceCollection.AddReceiptProcessingInfrastructure();
        using var provider = serviceCollection.BuildServiceProvider();

        Assert.IsType<DisabledOcrProviderClient>(
            provider.GetRequiredService<
                FinancialAssistant.ReceiptProcessing.Application.Abstractions.IOcrProviderClient>());
        Assert.IsType<ResilientOcrProvider>(
            provider.GetRequiredService<
                FinancialAssistant.ReceiptProcessing.Application.Abstractions.IOcrProvider>());
    }

    [Fact]
    public void Startup_RejectsInvalidOcrResilienceSettings()
    {
        using var factory = new ReceiptProcessingWebApplicationFactory()
            .WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ReceiptProcessing:Ocr:RequestTimeoutSeconds"] = "0"
                        })));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Request timeout", exception.ToString(), StringComparison.Ordinal);
    }
}
