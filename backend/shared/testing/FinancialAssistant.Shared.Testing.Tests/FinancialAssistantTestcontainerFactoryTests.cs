using FinancialAssistant.Shared.Testing;
using Testcontainers.Elasticsearch;
using Testcontainers.Minio;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace FinancialAssistant.Shared.Testing.Tests;

public sealed class FinancialAssistantTestcontainerFactoryTests
{
    [Fact]
    public void Images_ArePinnedAndCoverLocalInfrastructureDependencies()
    {
        var images = new[]
        {
            TestcontainerDefaults.ElasticsearchImage,
            TestcontainerDefaults.RabbitMqImage,
            TestcontainerDefaults.RedisImage,
            TestcontainerDefaults.MinioImage
        };

        Assert.Equal(4, images.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            "elasticsearch:8.15.3",
            TestcontainerDefaults.ElasticsearchImage);
        Assert.Equal(
            "rabbitmq:3.13.7-management",
            TestcontainerDefaults.RabbitMqImage);
        Assert.Equal(
            "redis:7.4.7-alpine",
            TestcontainerDefaults.RedisImage);
        Assert.Equal(
            "minio/minio:RELEASE.2025-09-07T16-13-09Z",
            TestcontainerDefaults.MinioImage);
        Assert.All(images, image =>
        {
            Assert.Contains(':', image);
            Assert.False(
                image.EndsWith(":latest", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void FactoryContract_ProvidesEachSupportedContainerType()
    {
        var factoryType = typeof(IFinancialAssistantTestcontainerFactory);

        Assert.Equal(
            typeof(ElasticsearchContainer),
            factoryType.GetMethod("CreateElasticsearch")!.ReturnType);
        Assert.Equal(
            typeof(RabbitMqContainer),
            factoryType.GetMethod("CreateRabbitMq")!.ReturnType);
        Assert.Equal(
            typeof(RedisContainer),
            factoryType.GetMethod("CreateRedis")!.ReturnType);
        Assert.Equal(
            typeof(MinioContainer),
            factoryType.GetMethod("CreateMinio")!.ReturnType);
    }

    [Fact]
    public void Credentials_AreClearlySyntheticTestValues()
    {
        var credentials = new[]
        {
            TestcontainerDefaults.ElasticsearchPassword,
            TestcontainerDefaults.RabbitMqUsername,
            TestcontainerDefaults.RabbitMqPassword,
            TestcontainerDefaults.MinioUsername,
            TestcontainerDefaults.MinioPassword
        };

        Assert.All(credentials, credential =>
            Assert.Contains("test", credential, StringComparison.Ordinal));
    }
}
