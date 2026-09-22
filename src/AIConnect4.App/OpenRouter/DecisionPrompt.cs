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
    /// Numbered Choice option for chat or enum-style models: unique <c>[[COL:N]]</c> token, stack facts, and one-ply win/block.
    /// </summary>
    public static string Criterion(Decision decision, Column column) =>
        $"[[COL:{column}]] {NumberedStackFacts(decision, column)}{TacticSuffix(decision, column)}";

    /// <summary>
    /// System One Choice option: stack facts and one-ply win/block only. No column number and no <c>[[COL:N]]</c>.
    /// </summary>
    public static string CriterionForSystemOne(Decision decision, Column column) =>
        $"{SystemOneStackFacts(decision, column)}{TacticSuffix(decision, column)}";

    private static string NumberedStackFacts(Decision decision, Column column)
    {
        var discs = DiscsFromBottom(decision, column);
        return discs.Count == 0
            ? $"Column {column} is empty. A disc drops to row 0."
            : $"Column {column} has {discs.Count} disc(s) from the bottom: {string.Join('-', discs)}. Next disc lands on row {discs.Count}.";
    }

    private static string SystemOneStackFacts(Decision decision, Column column)
    {
        var discs = DiscsFromBottom(decision, column);
        return discs.Count == 0
            ? "Empty. A disc drops to row 0."
            : $"{discs.Count} disc(s) from the bottom: {string.Join('-', discs)}. Next disc lands on row {discs.Count}.";
    }

    private static List<char> DiscsFromBottom(Decision decision, Column column)
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

        return discs;
    }

    private static string TacticSuffix(Decision decision, Column column)
    {
        if (decision.Board.Drop(decision.YouAre, column)?.Result is GameResult.Win)
        {
            return " Playing here wins immediately.";
        }

        if (decision.Board.Drop(decision.YouAre.Opponent(), column)?.Result is GameResult.Win)
        {
            return " Playing here blocks an immediate opponent win.";
        }

        return "";
    }

    /// <summary>System One Choice instructions: the shared question plus where to read the board in <c>state</c>.</summary>
    public static string ChoiceInstructions(Decision decision) =>
        $"{Decision.Question} Read state.board (top row first, R/Y/.) and state.last_move. " +
        $"You are {decision.YouAre.Disc()}. Each criterion describes one legal drop's stack and any host win or block marker.";

    /// <summary>One chat reminder after the legal list.</summary>
    public static string PlayReminder(Decision decision) =>
        $"Study the board grid above. You are {decision.YouAre.Disc()}. Pick one legal column.";

    /// <summary><see cref="MoveFailure.Describe"/> of <see cref="Decision.PriorFailure"/>. Null on a first attempt.</summary>
    public static string? PriorFailure(Decision decision) => decision.PriorFailure?.Describe();
}
