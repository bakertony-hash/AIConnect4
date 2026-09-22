using System.Collections.Immutable;

namespace AIConnect4.Core;

/// <summary>
/// The only thing a model is asked. One Choice question over the current board, with
/// <see cref="Criteria"/> equal to <see cref="Board.LegalColumns"/>. Both Jev and chat adapters consume this type.
/// </summary>
public sealed record Decision
{
    public const string Question = "Which legal column should you play?";

    public const string Rules =
        "Connect 4 on a 6-row by 7-column board. Players alternate dropping one disc into a column. " +
        "The disc falls to the lowest empty cell. Four of your discs in a row horizontally, vertically, or diagonally wins. " +
        "A full board with no four in a row is a draw. Columns are numbered 1 through 7 from left to right.";

    private Decision(Board board, Player youAre, MoveFailure? priorFailure)
    {
        Board = board;
        YouAre = youAre;
        Criteria = board.LegalColumns();
        PriorFailure = priorFailure;
    }

    public Board Board { get; }

    public Player YouAre { get; }

    /// <summary>Exactly <see cref="Board.LegalColumns"/> for <see cref="Board"/>, 1 through 7 from left to right.</summary>
    public ImmutableArray<Column> Criteria { get; }

    /// <summary>Set only on the single retry, so the adapter can put the error in the prompt.</summary>
    public MoveFailure? PriorFailure { get; }

    public PlacedDisc? LastMove => Board.LastMove;

    public string Grid => Board.Render();

    /// <summary>Builds the decision for <paramref name="toMove"/>. Throws when the board already has a <see cref="Board.Result"/>.</summary>
    public static Decision For(Board board, Player toMove) => throw new NotImplementedException();

    public Decision Retry(MoveFailure failure) => new(Board, YouAre, failure);

    public bool Allows(Column column) => Criteria.Contains(column);
}
