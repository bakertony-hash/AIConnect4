using System.Collections.Immutable;

namespace AIConnect4.App.OpenRouter;

/// <summary>The curated dropdown rows plus the rule that classifies a custom model id.</summary>
public static class ModelCatalog
{
    public static ModelProfile.SystemOne Jev { get; } = new("Jev 1.13", "typesafe/jev-1.13");

    public static ModelProfile.SystemOne JevLatest { get; } = new("Jev Latest", "~typesafe/jev-latest");

    public static ModelProfile.Chat Luna { get; } =
        new("GPT-5.6 Luna", "openai/gpt-5.6-luna", SupportsEffort: true, SupportsSpeed: true, AnswerFormat.JsonSchema);

    public static ImmutableArray<ModelProfile> Curated { get; } =
    [
        Jev,
        JevLatest,
        Luna,
        new ModelProfile.Chat("GPT-6 Luna", "openai/gpt-6-luna", SupportsEffort: true, SupportsSpeed: true, AnswerFormat.JsonSchema),
        new ModelProfile.Chat("GPT-6 Sol", "openai/gpt-6-sol", SupportsEffort: true, SupportsSpeed: true, AnswerFormat.JsonSchema),
        new ModelProfile.Chat("GPT-6 Astra", "openai/gpt-6-astra", SupportsEffort: true, SupportsSpeed: true, AnswerFormat.JsonSchema),
    ];

    public static ModelProfile DefaultRed => Jev;

    public static ModelProfile DefaultYellow => Luna;

    /// <summary>The profile for a model id typed into the Custom row.</summary>
    public static ModelProfile Custom(string modelId)
    {
        var id = modelId.Trim();
        return IsSystemOneId(id)
            ? new ModelProfile.SystemOne(id, id)
            : new ModelProfile.Chat(id, id, SupportsEffort: true, SupportsSpeed: true, AnswerFormat.Text);
    }

    public static bool IsSystemOneId(string modelId) =>
        modelId.StartsWith("typesafe/", StringComparison.Ordinal) || modelId.StartsWith("~typesafe/", StringComparison.Ordinal);
}
