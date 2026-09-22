using System.Text.Json.Nodes;
using AIConnect4.Core;

namespace AIConnect4.App.OpenRouter;

/// <summary>
/// Asks a chat model the same <see cref="Decision"/> as one single-turn <c>POST v1/chat/completions</c>. The system
/// message carries the rules and the side. The user message carries the board, last move, legal columns, prior failure,
/// and the question. The answer is <c>{ "column": n, "reason": "..." }</c> via <c>response_format</c> when the profile's
/// <see cref="AnswerFormat"/> allows it, else the column number alone. <c>reasoning.effort</c> and <c>provider.sort</c>
/// are sent only when the profile supports them and the <see cref="ChatTuning"/> sets them.
/// </summary>
public sealed class ChatMoveSource : IMoveSource
{
    public const string Path = "v1/chat/completions";

    private readonly OpenRouterClient _client;
    private readonly ModelProfile.Chat _profile;
    private readonly ChatTuning _tuning;

    public ChatMoveSource(OpenRouterClient client, ModelProfile.Chat profile, ChatTuning tuning)
    {
        _client = client;
        _profile = profile;
        _tuning = tuning;
    }

    public Task<MoveReply> GetMoveAsync(Decision decision, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private sealed record ChatRequest(string Model, ChatMessage[] Messages, ResponseFormat? ResponseFormat, Reasoning? Reasoning, Provider? Provider);

    private sealed record ChatMessage(string Role, string Content);

    /// <summary><c>type</c> is "json_schema" with <see cref="JsonSchema"/> set, or "json_object" with it null.</summary>
    private sealed record ResponseFormat(string Type, JsonSchemaFormat? JsonSchema);

    private sealed record JsonSchemaFormat(string Name, bool Strict, JsonObject Schema);

    private sealed record Reasoning(string Effort);

    private sealed record Provider(string Sort);

    private sealed record ChatResponse(ChatChoice[]? Choices);

    private sealed record ChatChoice(ChatAnswer? Message);

    private sealed record ChatAnswer(string? Content);
}
