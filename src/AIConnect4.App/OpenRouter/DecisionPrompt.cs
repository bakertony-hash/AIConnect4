using AIConnect4.Core;

namespace AIConnect4.App.OpenRouter;

/// <summary>Pure text for one <see cref="Decision"/>.</summary>
public static class DecisionPrompt
{
    /// <summary>"You are Red (R). It is your move." and the equivalent for Yellow.</summary>
    public static string WhoYouAre(Decision decision) => $"You are {decision.YouAre} ({decision.YouAre.Disc()}). It is your move.";

    /// <summary>A legend line followed by <see cref="Decision.Grid"/>. Top row printed first, 'R', 'Y', '.'.</summary>
    public static string Board(Decision decision) => "Board, top row first. R = Red, Y = Yellow, . = empty.\n" + decision.Grid;

    /// <summary>"Yellow played column 4." Null when the board is empty.</summary>
    public static string? LastMove(Decision decision) =>
        decision.LastMove is { } move ? $"{move.Player} played column {move.Position.Column}." : null;

    /// <summary>"Legal columns: 2, 3, 4, 5, 6, 7." from <see cref="Decision.Criteria"/>, in order.</summary>
    public static string LegalColumns(Decision decision) => $"Legal columns: {string.Join(", ", decision.Criteria)}.";

    /// <summary><see cref="MoveFailure.Describe"/> of <see cref="Decision.PriorFailure"/>. Null on a first attempt.</summary>
    public static string? PriorFailure(Decision decision) => decision.PriorFailure?.Describe();
}
