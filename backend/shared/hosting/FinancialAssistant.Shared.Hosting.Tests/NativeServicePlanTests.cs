using FinancialAssistant.Shared.Hosting;
using Xunit;

namespace FinancialAssistant.Shared.Hosting.Tests;

public sealed class NativeServicePlanTests
{
    private const string InstallationId = "c104bcfb-912a-4ca9-ae87-18128ff1c537";

    [Fact]
    public void Plan_UsesProductOwnedPaths_VirtualAccount_BoundedRecovery_AndRetainsData()
    {
        var plan = Create();
        Assert.Equal("FinancialAssistant.income", plan.ServiceName);
        Assert.Equal(@"NT SERVICE\FinancialAssistant.income", plan.ServiceAccount);
        Assert.Equal(@"C:\Program Files\FinancialAssistant\releases\test-release\services\income\FinancialAssistant.Income.Api.exe", plan.ExecutablePath);
        Assert.Equal("--environment WindowsNative", plan.Arguments);
        Assert.Equal("AutomaticDelayedStart", plan.StartMode);
        Assert.Equal(new[] { 30, 60 }, plan.RecoveryRestartDelaysSeconds);
        Assert.Equal("None", plan.RecoveryAfterRepeatedFailures);
        Assert.Equal(new[] { "aspnet-runtime", "postgresql", "rabbitmq" }, plan.RequiredComponents);
        Assert.Equal(86400, plan.RecoveryResetSeconds);
        Assert.Equal(30, plan.GracefulStopSeconds);
        Assert.True(plan.UninstallRetainsData);
    }

    [Fact]
    public void Upgrade_PreservesOwnership_WithoutRequiringSameReleasePath()
    {
        var previous = Create();
        var upgraded = NativeServicePlan.Create("income", "next-release", InstallationId, "FinancialAssistant.Income.Api.exe",
            [new(previous.ServiceName, InstallationId, previous.ExecutablePath)]);
        Assert.Contains("next-release", upgraded.ExecutablePath);
    }

    [Theory]
    [InlineData("foreign-owner")]
    [InlineData("foreign-path")]
    [InlineData("traversal")]
    [InlineData("duplicate")]
    public void ForeignOrAmbiguousRegistration_IsRejected(string mutation)
    {
        var plan = Create();
        var registration = new ExistingNativeService(plan.ServiceName, InstallationId, plan.ExecutablePath);
        var existing = new List<ExistingNativeService> { registration };
        switch (mutation)
        {
            case "foreign-owner": existing[0] = registration with { InstallationId = Guid.NewGuid().ToString() }; break;
            case "foreign-path": existing[0] = registration with { ExecutablePath = @"C:\Other\FinancialAssistant.Income.Api.exe" }; break;
            case "traversal": existing[0] = registration with { ExecutablePath = plan.ExecutablePath.Replace("test-release", @"..\other") }; break;
            case "duplicate": existing.Add(registration); break;
        }

        Assert.Throws<InvalidOperationException>(() => Create(existing));
    }

    [Theory]
    [InlineData("../escape", InstallationId, "FinancialAssistant.Income.Api.exe")]
    [InlineData("con", InstallationId, "FinancialAssistant.Income.Api.exe")]
    [InlineData("com1", InstallationId, "FinancialAssistant.Income.Api.exe")]
    [InlineData("valid", "not-a-guid", "FinancialAssistant.Income.Api.exe")]
    [InlineData("valid", "00000000-0000-0000-0000-000000000000", "FinancialAssistant.Income.Api.exe")]
    [InlineData("valid", InstallationId, "..\\foreign.exe")]
    [InlineData("valid", InstallationId, "FinancialAssistant.Income.Api.exe --bad")]
    [InlineData("valid", InstallationId, "FinancialAssistant.Expense.Api.exe")]
    public void UnsafePlanInput_IsRejected(string releaseId, string installationId, string executableName) =>
        Assert.Throws<InvalidOperationException>(() => NativeServicePlan.Create("income", releaseId, installationId, executableName, []));

    private static NativeServicePlan Create(IReadOnlyList<ExistingNativeService>? existing = null) =>
        NativeServicePlan.Create("income", "test-release", InstallationId, "FinancialAssistant.Income.Api.exe", existing ?? []);
}
