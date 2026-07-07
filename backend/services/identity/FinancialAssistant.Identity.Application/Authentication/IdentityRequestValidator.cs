using System.ComponentModel.DataAnnotations;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Authentication;

internal static class IdentityRequestValidator
{
    private static readonly EmailAddressAttribute EmailValidator = new();
    private static readonly HashSet<string> SupportedPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "ios",
        "android",
        "web"
    };

    public static IReadOnlyDictionary<string, string[]> ValidateRegistration(RegisterAccountRequest request, string? idempotencyKey)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        ValidateEmail(request.Email, errors);
        ValidateNewPassword(request.Password, errors);
        ValidateClient(request.Client, errors);

        if (idempotencyKey is not null && (idempotencyKey.Length < 8 || idempotencyKey.Length > 128))
        {
            Add(errors, "idempotencyKey", "Idempotency key must contain between 8 and 128 characters.");
        }

        return Freeze(errors);
    }

    public static IReadOnlyDictionary<string, string[]> ValidateSignIn(SignInRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        ValidateEmail(request.Email, errors);

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 128)
        {
            Add(errors, "password", "Password is required and must not exceed 128 characters.");
        }

        ValidateClient(request.Client, errors);
        return Freeze(errors);
    }

    public static IReadOnlyDictionary<string, string[]> ValidateGoogleSignIn(GoogleSignInRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(request.IdToken) || request.IdToken.Length is < 64 or > 16384)
        {
            Add(errors, "idToken", "A Google ID token is required and must have a valid size.");
        }

        ValidateClient(request.Client, errors);
        return Freeze(errors);
    }

    public static IReadOnlyDictionary<string, string[]> ValidateAppleSignIn(AppleSignInRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(request.IdentityToken)
            || request.IdentityToken.Length is < 64 or > 16384)
        {
            Add(errors, "identityToken", "An Apple identity token is required and must have a valid size.");
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || request.Nonce.Length is < 16 or > 512)
        {
            Add(errors, "nonce", "A client-generated nonce containing between 16 and 512 characters is required.");
        }

        ValidateClient(request.Client, errors);
        return Freeze(errors);
    }

    private static void ValidateEmail(string? email, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320 || !EmailValidator.IsValid(email))
        {
            Add(errors, "email", "A valid email address is required.");
        }
    }

    private static void ValidateNewPassword(string? password, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrEmpty(password) || password.Length is < 12 or > 128)
        {
            Add(errors, "password", "Password must contain between 12 and 128 characters.");
            return;
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            Add(errors, "password", "Password must include upper-case, lower-case, numeric, and symbol characters.");
        }
    }

    private static void ValidateClient(IdentityClientContext? client, Dictionary<string, List<string>> errors)
    {
        if (client is null)
        {
            Add(errors, "client", "Client context is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(client.ClientInstanceId) || client.ClientInstanceId.Length is < 8 or > 128)
        {
            Add(errors, "client.clientInstanceId", "Client instance identifier must contain between 8 and 128 characters.");
        }

        if (string.IsNullOrWhiteSpace(client.Platform) || !SupportedPlatforms.Contains(client.Platform))
        {
            Add(errors, "client.platform", "Platform must be ios, android, or web.");
        }

        if (client.AppVersion?.Length > 64)
        {
            Add(errors, "client.appVersion", "Application version must not exceed 64 characters.");
        }
    }

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = new List<string>();
            errors[field] = messages;
        }

        messages.Add(message);
    }

    private static IReadOnlyDictionary<string, string[]> Freeze(Dictionary<string, List<string>> errors)
    {
        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }
}
