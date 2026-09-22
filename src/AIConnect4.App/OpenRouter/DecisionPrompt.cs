using AIConnect4.Core;
using BoardGrid = AIConnect4.Core.Board;

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

    /// <summary>
    /// One Choice option boundary for <paramref name="column"/>: empty or the bottom-up disc stack and landing row.
    /// Host-owned facts so a System One model need not count the grid.
    /// </summary>
    public static string Criterion(Decision decision, Column column)
    {
        var discs = new List<char>(BoardGrid.Rows);
        for (var row = 0; row < BoardGrid.Rows; row++)
        {
            if (decision.Board.Cell(row, column) is not { } player)
            {
                break;
            }

            discs.Add(player.Disc());
        }

        return discs.Count == 0
            ? $"Column {column} is empty. A disc drops to row 0."
            : $"Column {column} has {discs.Count} disc(s) from the bottom: {string.Join('-', discs)}. Next disc lands on row {discs.Count}.";
    }

    /// <summary>System One Choice instructions: the shared question plus where to read the board in <c>state</c>.</summary>
    public static string ChoiceInstructions(Decision decision) =>
        $"{Decision.Question} Read state.board (top row first, R/Y/.) and state.last_move. " +
        $"You are {decision.YouAre.Disc()}. Win if you can, otherwise block an immediate opponent four, otherwise prefer centre columns.";

    /// <summary>One chat reminder after the legal list. Still one column decision.</summary>
    public static string PlayReminder(Decision decision) =>
        $"Study the board grid above. You are {decision.YouAre.Disc()}. Win if you can, otherwise block an immediate opponent four, otherwise prefer centre columns.";

    /// <summary><see cref="MoveFailure.Describe"/> of <see cref="Decision.PriorFailure"/>. Null on a first attempt.</summary>
    public static string? PriorFailure(Decision decision) => decision.PriorFailure?.Describe();
}
