using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Identity.Infrastructure.Persistence;

internal static class IdentityRecordValidation
{
    internal static bool Hash(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool Id(string? value) => Guid.TryParseExact(value, "N", out _);
    internal static void Require(bool condition) { if (!condition) { throw new StorageUnavailableException(); } }
}
