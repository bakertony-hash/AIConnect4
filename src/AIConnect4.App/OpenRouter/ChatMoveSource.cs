using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AIConnect4.Core;

namespace AIConnect4.App.OpenRouter;

/// <summary>Asks a chat model the same <see cref="Decision"/> as one single-turn <c>POST v1/chat/completions</c>.</summary>
public sealed class ChatMoveSource : IMoveSource
{
    public const string Path = "v1/chat/completions";

    private const string JsonFormatLine = """Answer with JSON only: {"column": <one of the legal columns>, "reason": "<one sentence>"}.""";

    private const string TextFormatLine = "Answer with the column number only.";

    private static readonly Regex Integer = new(@"-?\d+", RegexOptions.Compiled);

    private readonly OpenRouterClient _client;
    private readonly ModelProfile.Chat _profile;
    private readonly ChatTuning _tuning;

    public ChatMoveSource(OpenRouterClient client, ModelProfile.Chat profile, ChatTuning tuning)
    {
        _client = client;
        _profile = profile;
        _tuning = tuning;
    }

    public async Task<MoveReply> GetMoveAsync(Decision decision, CancellationToken cancellationToken)
    {
        var request = new ChatRequest(
            _profile.ModelId,
            [
                new ChatMessage("system", $"{Decision.Rules}\n{DecisionPrompt.WhoYouAre(decision)}"),
                new ChatMessage("user", UserMessage(decision)),
            ],
            ResponseFormatFor(decision),
            _profile.SupportsEffort && _tuning.Effort is { } effort ? new Reasoning(effort.ToString().ToLowerInvariant()) : null,
            _profile.SupportsSpeed && _tuning.Speed != Speed.Default ? new Provider(_tuning.Speed == Speed.Throughput ? "throughput" : "latency") : null);

        return await _client.PostAsync(Path, request, cancellationToken) switch
        {
            CallOutcome.Failed failed => new MoveReply.Failed(failed.Failure),
            CallOutcome.Body body => Parse(body.Json, decision),
            _ => throw new UnreachableException(),
        };
    }

    private string UserMessage(Decision decision)
    {
        string?[] lines =
        [
            DecisionPrompt.Board(decision),
            DecisionPrompt.LastMove(decision),
            DecisionPrompt.PriorFailure(decision),
            DecisionPrompt.LegalColumns(decision),
            DecisionPrompt.PlayReminder(decision),
            Decision.Question,
            _profile.AnswerFormat == AnswerFormat.Text ? TextFormatLine : JsonFormatLine,
        ];
        return string.Join("\n", lines.Where(line => line is not null));
    }

    private ResponseFormat? ResponseFormatFor(Decision decision) => _profile.AnswerFormat switch
    {
        AnswerFormat.JsonSchema => new ResponseFormat("json_schema", new JsonSchemaFormat("column_choice", Strict: true, ColumnSchema(decision))),
        AnswerFormat.JsonObject => new ResponseFormat("json_object", null),
        AnswerFormat.Text => null,
        _ => throw new UnreachableException(),
    };

    private static JsonObject ColumnSchema(Decision decision) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["column"] = new JsonObject
            {
                ["type"] = "integer",
                ["enum"] = new JsonArray([.. decision.Criteria.Select(column => JsonValue.Create(column.Value))]),
            },
            ["reason"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray("column", "reason"),
        ["additionalProperties"] = false,
    };

    private static MoveReply Parse(string body, Decision decision)
    {
        ChatResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ChatResponse>(body, OpenRouterClient.Json);
        }
        catch (JsonException)
        {
            parsed = null;
        }

        if (parsed is null)
        {
            return new MoveReply.Failed(new MoveFailure.Unparseable(OpenRouterClient.Snippet(body)));
        }

        var content = parsed.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return new MoveReply.Failed(new MoveFailure.Empty());
        }

        var text = StripFence(content.Trim());
        return JsonAnswer(text, decision) ?? NumberAnswer(text, decision);
    }

    private static MoveReply? JsonAnswer(string text, Decision decision)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }

        if (node is not JsonObject obj || !obj.TryGetPropertyValue("column", out var columnNode))
        {
            return null;
        }

        var reason = obj["reason"] is JsonValue reasonValue && reasonValue.TryGetValue<string>(out var reasonText) ? reasonText : null;
        if (columnNode is JsonValue value && value.TryGetValue<int>(out var number))
        {
            return ColumnAnswer.Resolve(number, text, decision, reason);
        }

        if (columnNode is JsonValue quoted && quoted.TryGetValue<string>(out var digits)
            && int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return ColumnAnswer.Resolve(parsed, text, decision, reason);
        }

        return new MoveReply.Failed(new MoveFailure.Unparseable(text));
    }

    private static MoveReply NumberAnswer(string text, Decision decision) =>
        Integer.Match(text) is { Success: true } match && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? ColumnAnswer.Resolve(value, text, decision, match.Value == text ? null : text)
            : new MoveReply.Failed(new MoveFailure.Unparseable(text));

    private static string StripFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        lines.RemoveAt(0);
        if (lines.Count > 0 && lines[^1].TrimEnd() == "```")
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join("\n", lines).Trim();
    }

    private sealed record ChatRequest(string Model, ChatMessage[] Messages, ResponseFormat? ResponseFormat, Reasoning? Reasoning, Provider? Provider);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ResponseFormat(string Type, JsonSchemaFormat? JsonSchema);

    private sealed record JsonSchemaFormat(string Name, bool Strict, JsonObject Schema);

    private sealed record Reasoning(string Effort);

    private sealed record Provider(string Sort);

    private sealed record ChatResponse(ChatChoice[]? Choices);

    private sealed record ChatChoice(ChatAnswer? Message);

    private sealed record ChatAnswer(string? Content);
}
