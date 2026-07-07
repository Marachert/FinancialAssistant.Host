namespace FinancialAssistant.Identity.Contracts.Auth;

public static class IdentityApiRoutes
{
    public const string Base = "/auth/v1";

    public const string Register = Base + "/register";
    public const string SignIn = Base + "/sign-in";
    public const string GoogleSignIn = Base + "/providers/google/sign-in";
    public const string Refresh = Base + "/refresh";
    public const string Logout = Base + "/logout";
    public const string CurrentUser = Base + "/me";

    public const string RegisterRelative = "/register";
    public const string SignInRelative = "/sign-in";
    public const string GoogleSignInRelative = "/providers/google/sign-in";
    public const string RefreshRelative = "/refresh";
    public const string LogoutRelative = "/logout";
    public const string CurrentUserRelative = "/me";
}
