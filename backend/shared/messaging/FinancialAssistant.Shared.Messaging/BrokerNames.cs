using System.Globalization;

namespace FinancialAssistant.Shared.Messaging;

internal static class BrokerNames
{
    internal static void Segment(string value, int maximum = 48)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maximum ||
            value.Split('-').Any(part => part.Length == 0 || part.Any(c => c is not (>= 'a' and <= 'z' or >= '0' and <= '9'))))
        { throw new ArgumentException("Native broker names must be bounded canonical ASCII segments."); }
    }

    internal static void Identifier(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128 ||
            value.Any(c => c is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-')))
        { throw new ArgumentException("Native publication identifiers must be bounded technical identifiers."); }
    }

    internal static void EventType(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128) { throw new ArgumentException("Invalid native event type."); }
        var parts = value.Split('.');
        if (parts.Length != 3 || parts[2].Length < 2 || parts[2][0] != 'v' || parts[2][1] == '0' ||
            !int.TryParse(parts[2].AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1)
        { throw new ArgumentException("Native event types require an explicit canonical positive version."); }
        Segment(parts[0], 128); Segment(parts[1], 128);
    }
}
