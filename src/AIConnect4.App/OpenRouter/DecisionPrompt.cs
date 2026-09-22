using AIConnect4.Core;

namespace AIConnect4.App.OpenRouter;

/// <summary>
/// Pure text for one <see cref="Decision"/>. Both adapters describe the same board, side, last move, and prior failure.
/// Only the question's transport differs: Jev gets it as a Choice with criteria, chat gets it as prose plus a format rule.
/// </summary>
public static class DecisionPrompt
{
    /// <summary>"You are Red (R). It is your move." and the equivalent for Yellow.</summary>
    public static string WhoYouAre(Decision decision) => throw new NotImplementedException();

    /// <summary>A legend line followed by <see cref="Decision.Grid"/>. Top row printed first, 'R', 'Y', '.'.</summary>
    public static string Board(Decision decision) => throw new NotImplementedException();

    /// <summary>"Yellow played column 4." Null when the board is empty.</summary>
    public static string? LastMove(Decision decision) => throw new NotImplementedException();

    /// <summary>"Legal columns: 2, 3, 4, 5, 6, 7." from <see cref="Decision.Criteria"/>, in order.</summary>
    public static string LegalColumns(Decision decision) => throw new NotImplementedException();

    /// <summary><see cref="MoveFailure.Describe"/> of <see cref="Decision.PriorFailure"/>. Null on a first attempt.</summary>
    public static string? PriorFailure(Decision decision) => throw new NotImplementedException();
}
