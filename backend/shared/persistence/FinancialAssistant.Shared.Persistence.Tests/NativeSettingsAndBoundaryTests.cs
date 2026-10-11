using System.Text;
using FinancialAssistant.Shared.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinancialAssistant.Shared.Persistence.Tests;

public sealed class NativeSettingsAndBoundaryTests
{
    [Fact]
    public void ValidKeysAreCopiedAndInvalidMaterialFailsWithoutSensitiveDetails()
    {
        var settings = Settings();
        using var protection = settings.CreateProtection();
        var context = Encoding.UTF8.GetBytes("synthetic-profile-context");
        var encrypted = protection.Encrypt(Encoding.UTF8.GetBytes("synthetic"), context);
        Assert.Equal("synthetic", Encoding.UTF8.GetString(protection.Decrypt(encrypted, context)));
        settings.IndexKey = "invalid-base64";
        var failure = Assert.Throws<InvalidOperationException>(() => settings.CreateProtection());
        Assert.Null(failure.InnerException);
        Assert.DoesNotContain(settings.IndexKey, failure.Message);
        settings = Settings();
        settings.EncryptionKeys["v1"] = settings.IndexKey;
        Assert.Throws<InvalidOperationException>(() => settings.CreateProtection());
        settings = Settings();
        settings.ActiveEncryptionKeyId = "missing";
        Assert.Throws<InvalidOperationException>(() => settings.CreateProtection());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void InvalidNativePortFailsBeforeAnyConnection(int port)
    {
        var settings = Settings();
        settings.Port = port;
        Assert.Throws<ArgumentException>(() => settings.CreateOptions("profile"));
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("conflict")]
    [InlineData("deadline")]
    public async Task NativeBoundaryReturnsGeneric503(string kind)
    {
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseOwnedStorageFailureBoundary();
        app.Run(_ => throw kind switch
        {
            "conflict" => new StorageConflictException(),
            "deadline" => new OperationCanceledException(),
            _ => new StorageUnavailableException()
        });
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        await app.Build()(context);
        Assert.Equal(503, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("Service operation unavailable.", body);
        Assert.DoesNotContain("PostgreSQL", body);
    }

    [Fact]
    public async Task CallerCancellationIsNotConvertedToStorageFailure()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseOwnedStorageFailureBoundary();
        app.Run(_ => throw new OperationCanceledException());
        var context = new DefaultHttpContext { RequestServices = services, RequestAborted = new CancellationToken(true) };
        await Assert.ThrowsAsync<OperationCanceledException>(() => app.Build()(context));
    }

    private static NativePostgresSettings Settings() => new()
    {
        Database = "synthetic",
        Port = 5432,
        Password = new string('x', 40),
        IndexKey = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
        ActiveEncryptionKeyId = "v1",
        EncryptionKeys = new() { ["v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray()) }
    };
}
