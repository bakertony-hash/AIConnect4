using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AIConnect4.Core;

namespace AIConnect4.App.OpenRouter;

/// <summary>
/// Asks a System One model one Choice question per call over <c>POST v1/systemone</c>. Confidence and probabilities go
/// into the reply's <see cref="MoveReply.Chosen.Reason"/>.
/// </summary>
public sealed class JevMoveSource : IMoveSource
{
    public const string Path = "v1/systemone";

    public const string QuestionName = "column";

    private readonly OpenRouterClient _client;
    private readonly ModelProfile.SystemOne _profile;

    public JevMoveSource(OpenRouterClient client, ModelProfile.SystemOne profile)
    {
        _client = client;
        _profile = profile;
    }

    public async Task<MoveReply> GetMoveAsync(Decision decision, CancellationToken cancellationToken)
    {
        var request = new SystemOneRequest(
            _profile.ModelId,
            new DecisionState(
                Decision.Rules,
                DecisionPrompt.WhoYouAre(decision),
                DecisionPrompt.Board(decision),
                DecisionPrompt.LastMove(decision),
                DecisionPrompt.PriorFailure(decision)),
            new Dictionary<string, ChoiceQuestion>
            {
                [QuestionName] = new ChoiceQuestion(
                    DecisionPrompt.ChoiceInstructions(decision),
                    decision.Criteria.ToDictionary(column => column.ToString(), column => DecisionPrompt.Criterion(decision, column))),
            });

        return await _client.PostAsync(Path, request, cancellationToken) switch
        {
            CallOutcome.Failed failed => new MoveReply.Failed(failed.Failure),
            CallOutcome.Body body => Parse(body.Json, decision),
            _ => throw new UnreachableException(),
        };
    }

    private static MoveReply Parse(string body, Decision decision)
    {
        ChoiceAnswer? answer;
        try
        {
            answer = JsonSerializer.Deserialize<SystemOneResponse>(body, OpenRouterClient.Json)?.Answers?.GetValueOrDefault(QuestionName);
        }
        catch (JsonException)
        {
            answer = null;
        }

        if (answer is null)
        {
            return new MoveReply.Failed(new MoveFailure.Unparseable(OpenRouterClient.Snippet(body)));
        }

        if (string.IsNullOrWhiteSpace(answer.Choice))
        {
            return new MoveReply.Failed(new MoveFailure.Empty());
        }

        return int.TryParse(answer.Choice.Trim(), out var value)
            ? ColumnAnswer.Resolve(value, answer.Choice, decision, Reason(answer))
            : new MoveReply.Failed(new MoveFailure.Unparseable(answer.Choice));
    }

    private static string? Reason(ChoiceAnswer answer)
    {
        var parts = new List<string>(2);
        if (answer.Confidence is { } confidence)
        {
            parts.Add($"confidence {confidence.ToString("0.00", CultureInfo.InvariantCulture)}");
        }

        if (answer.Probabilities is { } probabilities)
        {
            var ordered = probabilities
                .OrderBy(pair => int.TryParse(pair.Key, out var key) ? key : int.MaxValue)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}: {pair.Value.ToString(CultureInfo.InvariantCulture)}");
            parts.Add(string.Join(", ", ordered));
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private sealed record SystemOneRequest(string Model, DecisionState State, Dictionary<string, ChoiceQuestion> Questions);

    private sealed record DecisionState(string Rules, string YouAre, string Board, string? LastMove, string? PreviousAttempt);

    private sealed record ChoiceQuestion(string Instructions, Dictionary<string, string> Criteria)
    {
        public string Type => "choice";
    }

    private sealed record SystemOneResponse(Dictionary<string, ChoiceAnswer>? Answers);

    private sealed record ChoiceAnswer(string? Type, string? Choice, double? Confidence, Dictionary<string, double>? Probabilities);
}
