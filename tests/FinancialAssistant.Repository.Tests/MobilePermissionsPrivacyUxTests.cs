using System.Text.Json;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class MobilePermissionsPrivacyUxTests
{
    [Fact]
    public void ReceiptCapture_UsesContextualCameraAndGalleryPermissionFlow()
    {
        var add = ReadRequiredFile("mobile/app-react-native/src/app/(app)/add.tsx");

        Assert.Contains("ImagePicker.getCameraPermissionsAsync()", add, StringComparison.Ordinal);
        Assert.Contains("ImagePicker.requestCameraPermissionsAsync()", add, StringComparison.Ordinal);
        Assert.Contains("ImagePicker.getMediaLibraryPermissionsAsync()", add, StringComparison.Ordinal);
        Assert.Contains("ImagePicker.requestMediaLibraryPermissionsAsync()", add, StringComparison.Ordinal);
        Assert.Contains("ImagePicker.launchImageLibraryAsync", add, StringComparison.Ordinal);
        Assert.Contains("t('permissions.cameraRationale')", add, StringComparison.Ordinal);
        Assert.Contains("t('permissions.galleryRationale')", add, StringComparison.Ordinal);
        Assert.Contains("t('permissions.receiptPrivacy')", add, StringComparison.Ordinal);
        Assert.Contains("t('permissions.openSettings')", add, StringComparison.Ordinal);
        Assert.Contains("t('permissions.useFiles')", add, StringComparison.Ordinal);
        Assert.Contains("permission.canAskAgain", add, StringComparison.Ordinal);
        Assert.Contains("await Linking.openSettings()", add, StringComparison.Ordinal);
        Assert.Contains("DocumentPicker.getDocumentAsync", add, StringComparison.Ordinal);
    }

    [Fact]
    public void NotificationPermission_IsExplainedOptionalAndRefreshedAfterSettings()
    {
        var settings = ReadRequiredFile("mobile/app-react-native/src/app/(app)/settings.tsx");
        var onboarding = ReadRequiredFile("mobile/app-react-native/src/app/(app)/onboarding.tsx");

        Assert.Contains("notificationRationaleVisible", settings, StringComparison.Ordinal);
        Assert.Contains("t('permissions.notificationRationale')", settings, StringComparison.Ordinal);
        Assert.Contains("Notifications.requestPermissionsAsync()", settings, StringComparison.Ordinal);
        Assert.Contains("AppState.addEventListener('change'", settings, StringComparison.Ordinal);
        Assert.Contains("if (nextState === 'active') void loadDevicePermission()", settings, StringComparison.Ordinal);
        Assert.Contains("await Linking.openSettings()", settings, StringComparison.Ordinal);
        Assert.Contains("t('permissions.notificationOptional')", onboarding, StringComparison.Ordinal);
        Assert.Contains("budgetNotificationsEnabled: notificationsGranted", onboarding, StringComparison.Ordinal);
        Assert.Contains("weeklySummaryNotificationsEnabled: notificationsGranted", onboarding, StringComparison.Ordinal);
    }

    [Fact]
    public void NativePermissionCopy_DescribesUserSelectedReceiptPurpose()
    {
        using var app = JsonDocument.Parse(ReadRequiredFile("mobile/app-react-native/app.json"));
        var imagePicker = app.RootElement.GetProperty("expo").GetProperty("plugins")
            .EnumerateArray()
            .Single(plugin => plugin.ValueKind == JsonValueKind.Array
                && plugin[0].GetString() == "expo-image-picker")[1];

        var cameraCopy = imagePicker.GetProperty("cameraPermission").GetString();
        var photoCopy = imagePicker.GetProperty("photosPermission").GetString();

        Assert.Contains("receipt", cameraCopy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("choose", cameraCopy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("upload", cameraCopy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("receipt", photoCopy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("choose", photoCopy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("upload", photoCopy, StringComparison.OrdinalIgnoreCase);
        Assert.False(imagePicker.GetProperty("microphonePermission").GetBoolean());
    }

    [Fact]
    public void ReleasePrivacyText_MatchesReceiptAndPermissionBehavior()
    {
        var policy = ReadRequiredFile("docs/legal/privacy-policy.md");
        using var disclosures = JsonDocument.Parse(
            ReadRequiredFile("mobile/app-react-native/store/privacy-disclosures.json"));
        var photos = disclosures.RootElement.GetProperty("googlePlay")
            .EnumerateArray()
            .Single(item => item.GetProperty("dataType").GetString() == "Photos");

        Assert.Contains(
            "Camera, photo-library, and notification access is requested only in context",
            policy,
            StringComparison.Ordinal);
        Assert.Contains("authoritative financial records", policy, StringComparison.Ordinal);
        Assert.Contains("user review and confirmation", policy, StringComparison.Ordinal);
        Assert.True(photos.GetProperty("collected").GetBoolean());
        Assert.False(photos.GetProperty("shared").GetBoolean());
        Assert.False(photos.GetProperty("required").GetBoolean());
    }

    private static string ReadRequiredFile(string path)
    {
        var fullPath = Path.Combine(FindRepositoryRoot(), path.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Required FIN-188 file '{path}' is missing.");
        return File.ReadAllText(fullPath);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing FinancialAssistant.Backend.sln.");
    }
}
