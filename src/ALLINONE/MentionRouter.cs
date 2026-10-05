namespace ALLINONE;

public sealed record MentionOption(string Key, string Label, string Description);

public static class MentionRouter
{
    public static IReadOnlyList<MentionOption> Options { get; } =
    [
        new("SearchInOne", "@SearchInOne", "Real internet search with source results"),
        new("CodeInOne", "@CodeInOne", "Coding-focused local-model routing"),
        new("MathInOne", "@MathInOne", "Local calculator and math routing"),
        new("Project", "@Project", "Current project/workspace context"),
        new("File", "@File", "Reference a local file"),
        new("Website", "@Website", "Retrieve permitted public website text"),
        new("YouTube", "@YouTube", "Retrieve public video metadata"),
        new("Model", "@Model", "Inspect or switch the active model")
    ];

    public static (string? target, string text) Parse(string input)
    {
        var trimmed = input.TrimStart();

        foreach (var option in Options)
        {
            if (trimmed.StartsWith(option.Label, StringComparison.OrdinalIgnoreCase))
                return (option.Key, trimmed[option.Label.Length..].Trim());
        }

        return (null, input);
    }
}
