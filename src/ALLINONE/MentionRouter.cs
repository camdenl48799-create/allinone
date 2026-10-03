namespace ALLINONE;

public sealed record MentionOption(string Key, string Label, string Description);

public static class MentionRouter
{
    public static IReadOnlyList<MentionOption> Options { get; } =
    [
        new("SearchInOne", "@SearchInOne", "Real internet search with source results"),
        new("CodeInOne", "@CodeInOne", "Coding-focused routing"),
        new("Project", "@Project", "Current project/workspace context"),
        new("File", "@File", "Reference a local file"),
        new("Website", "@Website", "Retrieve permitted website text"),
        new("YouTube", "@YouTube", "Retrieve available public video metadata"),
        new("Model", "@Model", "Select or inspect the active model")
    ];

    public static (string? target, string text) Parse(string input)
    {
        foreach (var option in Options)
        {
            if (input.StartsWith(option.Label, StringComparison.OrdinalIgnoreCase))
                return (option.Key, input[option.Label.Length..].Trim());
        }
        return (null, input);
    }
}
