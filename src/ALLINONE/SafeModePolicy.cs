using System.Text.RegularExpressions;

namespace ALLINONE;

public static class SafeModePolicy
{
    private static readonly string[] BlockedPhrases =
    [
        "how to hurt someone",
        "self harm",
        "self-harm",
        "selfharm",
        "hurt myself",
        "suicide"
    ];

    public static bool IsBlocked(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var normalized = Regex.Replace(input, @"\s+", " ").Trim();

        return BlockedPhrases.Any(
            phrase => normalized.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }
}
