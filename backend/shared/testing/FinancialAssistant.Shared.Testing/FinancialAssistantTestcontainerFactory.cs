using Testcontainers.Elasticsearch;
using Testcontainers.Minio;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace FinancialAssistant.Shared.Testing;

public interface IFinancialAssistantTestcontainerFactory
{
    ElasticsearchContainer CreateElasticsearch();

    RabbitMqContainer CreateRabbitMq();

    RedisContainer CreateRedis();

    MinioContainer CreateMinio();
}

public sealed class FinancialAssistantTestcontainerFactory :
    IFinancialAssistantTestcontainerFactory
{
    public ElasticsearchContainer CreateElasticsearch() =>
        new ElasticsearchBuilder(TestcontainerDefaults.ElasticsearchImage)
            .WithPassword(TestcontainerDefaults.ElasticsearchPassword)
            .Build();

    public RabbitMqContainer CreateRabbitMq() =>
        new RabbitMqBuilder(TestcontainerDefaults.RabbitMqImage)
            .WithUsername(TestcontainerDefaults.RabbitMqUsername)
            .WithPassword(TestcontainerDefaults.RabbitMqPassword)
            .Build();

    public RedisContainer CreateRedis() =>
        new RedisBuilder(TestcontainerDefaults.RedisImage)
            .Build();

    public MinioContainer CreateMinio() =>
        new MinioBuilder(TestcontainerDefaults.MinioImage)
            .WithUsername(TestcontainerDefaults.MinioUsername)
            .WithPassword(TestcontainerDefaults.MinioPassword)
            .Build();
}

public static class TestcontainerDefaults
{
    public const string ElasticsearchImage = "elasticsearch:8.15.3";
    public const string RabbitMqImage = "rabbitmq:3.13.7-management";
    public const string RedisImage = "redis:7.4.7-alpine";
    public const string MinioImage =
        "minio/minio:RELEASE.2025-09-07T16-13-09Z";

    public const string ElasticsearchPassword = "fa_test_elastic_dev_only";
    public const string RabbitMqUsername = "fa_test";
    public const string RabbitMqPassword = "fa_test_rabbit_dev_only";
    public const string MinioUsername = "fa_test_minio";
    public const string MinioPassword = "fa_test_minio_dev_only";
}
