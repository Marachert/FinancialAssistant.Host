namespace FinancialAssistant.Profile.Contracts;

public static class ProfileApiRoutes
{
    public const string CurrentProfile = "/users/me";
    public const string CurrentProfilePreferences = "/users/me/preferences";
    public const string UserRegisteredEvent = "/internal/profile/v1/events/user-registered";
}
