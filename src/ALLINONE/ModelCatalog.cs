namespace ALLINONE;

public sealed record ModelDescriptor(string Id, string DisplayName, string Family, string Description, bool Available, string? Reason = null);

public static class ModelCatalog
{
    public static IReadOnlyList<ModelDescriptor> All { get; } =
    [
        new("local-core", "Local Core", "ALLINONE", "Built-in local tools and deterministic responses. No external model provider.", true),
        new("allinone-4.5", "ALLINONE 4.5", "ALLINONE", "Roadmap model. Not installed in this build.", false, "Model runtime is not connected."),
        new("codeinone-4.6", "CodeInOne 4.6", "CodeInOne", "Roadmap coding model. Not installed in this build.", false, "Model runtime is not connected."),
        new("codeinone-4.7", "CodeInOne 4.7", "CodeInOne", "Roadmap coding model. Not installed in this build.", false, "Model runtime is not connected."),
        new("mathinone-4.8", "MathInOne 4.8", "MathInOne", "Roadmap mathematics model. Not installed in this build.", false, "Model runtime is not connected."),
        new("mathinone-4.9", "MathInOne 4.9", "MathInOne", "Roadmap mathematics model. Not installed in this build.", false, "Model runtime is not connected."),
        new("one-3.x", "One-3.x", "ALLINONE", "Future model family. Availability depends on a future release.", false, "Future roadmap entry."),
        new("one-2", "One-2", "ALLINONE", "Planned model family. Not installed in this build.", false, "Model runtime is not connected."),
        new("g-one", "G-One", "G-One", "Combined multi-capability model concept. Not implemented in this build.", false, "Not implemented.")
    ];

    public static ModelDescriptor Get(string? id) =>
        All.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? All[0];
}
