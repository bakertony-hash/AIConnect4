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
    private readonly Random _random;

    public JevMoveSource(OpenRouterClient client, ModelProfile.SystemOne profile, Random? random = null)
    {
        _client = client;
        _profile = profile;
        _random = random ?? Random.Shared;
    }

    public async Task<MoveReply> GetMoveAsync(Decision decision, CancellationToken cancellationToken)
    {
        var assignment = OpaqueCriteria.Assign(decision, _random);
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
                [QuestionName] = new ChoiceQuestion(DecisionPrompt.ChoiceInstructions(decision), assignment.Criteria),
            });

        return await _client.PostAsync(Path, request, cancellationToken) switch
        {
            CallOutcome.Failed failed => new MoveReply.Failed(failed.Failure),
            CallOutcome.Body body => Parse(body.Json, decision, assignment.KeyToColumn),
            _ => throw new UnreachableException(),
        };
    }

    private static MoveReply Parse(string body, Decision decision, IReadOnlyDictionary<string, Column> keyToColumn)
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

        var key = answer.Choice.Trim();
        return keyToColumn.TryGetValue(key, out var column)
            ? ColumnAnswer.Resolve(column.Value, answer.Choice, decision, Reason(answer))
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
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}: {pair.Value.ToString(CultureInfo.InvariantCulture)}");
            parts.Add(string.Join(", ", ordered));
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// Opaque Choice keys in shuffled insertion order so System One positional bias cannot agree on a column index.
    /// </summary>
    internal static class OpaqueCriteria
    {
        public static Assignment Assign(Decision decision, Random random)
        {
            var columns = decision.Criteria.ToArray();
            Shuffle(columns, random);

            var criteria = new Dictionary<string, string>(columns.Length);
            var keyToColumn = new Dictionary<string, Column>(columns.Length);
            for (var index = 0; index < columns.Length; index++)
            {
                var key = $"opt_{(char)('a' + index)}";
                var column = columns[index];
                criteria[key] = DecisionPrompt.CriterionForSystemOne(decision, column);
                keyToColumn[key] = column;
            }

            return new Assignment(criteria, keyToColumn);
        }

        private static void Shuffle(Column[] columns, Random random)
        {
            for (var index = columns.Length - 1; index > 0; index--)
            {
                var swap = random.Next(index + 1);
                (columns[index], columns[swap]) = (columns[swap], columns[index]);
            }
        }

        public sealed record Assignment(
            Dictionary<string, string> Criteria,
            Dictionary<string, Column> KeyToColumn);
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
