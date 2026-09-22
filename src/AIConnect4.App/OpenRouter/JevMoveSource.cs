using AIConnect4.Core;

namespace AIConnect4.App.OpenRouter;

/// <summary>
/// Asks a System One model one Choice question per call over <c>POST v1/systemone</c>. The request is
/// <c>{ model, state, questions: { column: { type: "choice", instructions, criteria } } }</c> where <c>criteria</c>
/// maps each legal column ("1" through "7") to a short description. The reply's <c>answers.column.choice</c> is the column.
/// The profile type rules out <c>reasoning.effort</c> and <c>provider.sort</c>. Confidence and probabilities go into the
/// reply's <see cref="MoveReply.Chosen.Reason"/> for the side panel.
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

    public Task<MoveReply> GetMoveAsync(Decision decision, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private sealed record SystemOneRequest(string Model, DecisionState State, Dictionary<string, ChoiceQuestion> Questions);

    /// <summary>The shared decision payload as an object, not prose. Snake_case on the wire.</summary>
    private sealed record DecisionState(string Rules, string YouAre, string Board, string? LastMove, string? PreviousAttempt);

    private sealed record ChoiceQuestion(string Instructions, Dictionary<string, string> Criteria)
    {
        public string Type => "choice";
    }

    private sealed record SystemOneResponse(Dictionary<string, ChoiceAnswer>? Answers);

    private sealed record ChoiceAnswer(string? Type, string? Choice, double? Confidence, Dictionary<string, double>? Probabilities);
}
