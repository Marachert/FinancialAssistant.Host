using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Domain;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

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
            typeof(ReceiptOcrMetadata)
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
}
