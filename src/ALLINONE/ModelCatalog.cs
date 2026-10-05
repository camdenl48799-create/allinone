namespace ALLINONE;

public sealed record ModelDescriptor(
    string Id,
    string DisplayName,
    string Family,
    string Description,
    bool Available,
    string? Reason = null);

public static class ModelCatalog
{
    public static IReadOnlyList<ModelDescriptor> All { get; } =
    [
        new(
            "local-core",
            "Local Core",
            "ALLINONE",
            "Built-in deterministic tools: math, routing, projects, files, and search.",
            true),
        new(
            "local-model",
            "Local Model",
            "ALLINONE",
            "Optional local inference through an Ollama or OpenAI-compatible endpoint.",
            true),
        new(
            "allinone-4.5",
            "ALLINONE 4.5",
            "ALLINONE",
            "Roadmap model. Not installed in this build.",
            false,
            "Model runtime is not connected."),
        new(
            "codeinone-4.6",
            "CodeInOne 4.6",
            "CodeInOne",
            "Roadmap coding model.",
            false,
            "Model runtime is not connected."),
        new(
            "codeinone-4.7",
            "CodeInOne 4.7",
            "CodeInOne",
            "Roadmap coding model.",
            false,
            "Model runtime is not connected."),
        new(
            "mathinone-4.8",
            "MathInOne 4.8",
            "MathInOne",
            "Roadmap mathematics model.",
            false,
            "Model runtime is not connected."),
        new(
            "mathinone-4.9",
            "MathInOne 4.9",
            "MathInOne",
            "Roadmap mathematics model.",
            false,
            "Model runtime is not connected."),
        new(
            "one-3.x",
            "One-3.x",
            "ALLINONE",
            "Future model family.",
            false,
            "Future roadmap entry."),
        new(
            "one-2",
            "One-2",
            "ALLINONE",
            "Planned model family.",
            false,
            "Model runtime is not connected."),
        new(
            "g-one",
            "G-One",
            "G-One",
            "Combined multi-capability model concept.",
            false,
            "Not implemented.")
    ];

    public static ModelDescriptor Get(string? id) =>
        All.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? All[0];
}
