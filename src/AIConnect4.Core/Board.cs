using System.Collections.Immutable;

namespace AIConnect4.Core;

/// <summary>Immutable 6x7 grid. Row 0 is the bottom row.</summary>
public sealed class Board
{
    public const int Rows = 6;
    public const int Columns = Column.Count;

    private readonly Player?[] _cells;

    private Board(Player?[] cells, PlacedDisc? lastMove, GameResult? result)
    {
        _cells = cells;
        LastMove = lastMove;
        Result = result;
    }

    public static Board Empty { get; } = new(new Player?[Rows * Columns], lastMove: null, result: null);

    public PlacedDisc? LastMove { get; }

    /// <summary>Null while the game continues. Once set, <see cref="LegalColumns"/> is empty and <see cref="Drop"/> rejects every column.</summary>
    public GameResult? Result { get; }

    public int DiscCount => _cells.Count(cell => cell is not null);

    public Player? this[BoardPosition position] => _cells[CellIndex(position.Row, position.Column)];

    public Player? Cell(int row, Column column) => _cells[CellIndex(row, column)];

    /// <summary>Columns 1 through 7, left to right, that still accept a disc. Empty once the game has a result.</summary>
    public ImmutableArray<Column> LegalColumns() => throw new NotImplementedException();

    /// <summary>The board after <paramref name="player"/> drops into <paramref name="column"/>, or null when the column is not legal.</summary>
    public Board? Drop(Player player, Column column) => throw new NotImplementedException();

    /// <summary>Six lines of seven characters, top row first, using 'R', 'Y', and '.'.</summary>
    public string Render() => throw new NotImplementedException();

    private static int CellIndex(int row, Column column) => row * Columns + column.Index;
}

public readonly record struct BoardPosition(int Row, Column Column);

public readonly record struct PlacedDisc(Player Player, BoardPosition Position);

public sealed record WinningLine(BoardPosition First, BoardPosition Second, BoardPosition Third, BoardPosition Fourth)
{
    public IEnumerable<BoardPosition> Cells => [First, Second, Third, Fourth];
}

public abstract record GameResult
{
    private GameResult()
    {
    }

    public sealed record Win(Player Winner, WinningLine Line) : GameResult;

    public sealed record Draw : GameResult;
}
