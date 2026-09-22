namespace AIConnect4.App.OpenRouter;

/// <summary>Which OpenRouter endpoint answers for a model and which knobs it accepts.</summary>
public abstract record ModelProfile
{
    private ModelProfile(string displayName, string modelId)
    {
        DisplayName = displayName;
        ModelId = modelId;
    }

    public string DisplayName { get; }

    public string ModelId { get; }

    /// <summary>Answers one Choice question per call over <c>POST v1/systemone</c>.</summary>
    public sealed record SystemOne(string DisplayName, string ModelId) : ModelProfile(DisplayName, ModelId);

    /// <summary>Answers the same decision as a single-turn <c>POST v1/chat/completions</c>.</summary>
    public sealed record Chat(string DisplayName, string ModelId, bool SupportsEffort, bool SupportsSpeed, AnswerFormat AnswerFormat)
        : ModelProfile(DisplayName, ModelId);
}

/// <summary>How a chat model is asked to answer.</summary>
public enum AnswerFormat
{
    /// <summary><c>response_format.json_schema</c> whose <c>column</c> enum is exactly the decision's criteria.</summary>
    JsonSchema,

    /// <summary><c>response_format.json_object</c>. The allowed columns are stated in the prompt only.</summary>
    JsonObject,

    /// <summary>No <c>response_format</c>. The model is asked for the column number alone.</summary>
    Text,
}

/// <summary>OpenRouter <c>reasoning.effort</c> values.</summary>
public enum Effort
{
    None,
    Minimal,
    Low,
    Medium,
    High,
    XHigh,
}

/// <summary>OpenRouter <c>provider.sort</c>. <see cref="Default"/> omits the field.</summary>
public enum Speed
{
    Default,
    Throughput,
    LowLatency,
}

/// <summary>The user's per-side knobs for a <see cref="ModelProfile.Chat"/>. A null <see cref="Effort"/> omits the field.</summary>
public sealed record ChatTuning(Effort? Effort, Speed Speed)
{
    public static ChatTuning Omit { get; } = new(Effort: null, Speed.Default);
}
