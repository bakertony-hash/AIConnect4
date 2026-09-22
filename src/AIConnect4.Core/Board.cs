using System.Collections.Immutable;

namespace AIConnect4.Core;

/// <summary>Immutable 6x7 grid. Row 0 is the bottom row.</summary>
public sealed class Board
{
    public const int Rows = 6;
    public const int Columns = Column.Count;

    private static readonly (int RowStep, int ColumnStep)[] Directions = [(0, 1), (1, 0), (1, 1), (1, -1)];

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

    public Player? this[BoardPosition position] => _cells[CellIndex(position)];

    public Player? Cell(int row, Column column) => _cells[CellIndex(row, column)];

    /// <summary>Columns 1 through 7, left to right, that still accept a disc. Empty once the game has a result.</summary>
    public ImmutableArray<Column> LegalColumns() =>
        Result is null ? [.. Column.All.Where(column => Cell(Rows - 1, column) is null)] : [];

    /// <summary>The board after <paramref name="player"/> drops into <paramref name="column"/>, or null when the column is not legal.</summary>
    public Board? Drop(Player player, Column column)
    {
        if (!LegalColumns().Contains(column))
        {
            return null;
        }

        var row = Enumerable.Range(0, Rows).First(candidate => Cell(candidate, column) is null);
        var cells = _cells.ToArray();
        cells[CellIndex(row, column)] = player;
        var disc = new PlacedDisc(player, new BoardPosition(row, column));
        GameResult? result = WinningLineThrough(cells, disc) is { } line ? new GameResult.Win(player, line)
            : cells.Contains(null) ? null
            : new GameResult.Draw();
        return new Board(cells, disc, result);
    }

    /// <summary>Six lines of seven characters, top row first, using 'R', 'Y', and '.'.</summary>
    public string Render() =>
        string.Join('\n', Enumerable.Range(0, Rows).Reverse().Select(row =>
            string.Concat(Column.All.Select(column => Cell(row, column)?.Disc() ?? '.'))));

    private static WinningLine? WinningLineThrough(Player?[] cells, PlacedDisc disc)
    {
        foreach (var (rowStep, columnStep) in Directions)
        {
            var behind = Run(cells, disc, -rowStep, -columnStep).Reverse().ToList();
            var run = behind.Append(disc.Position).Concat(Run(cells, disc, rowStep, columnStep)).ToList();
            if (run.Count >= 4)
            {
                var lastWindowStartHoldingDisc = Math.Min(behind.Count, run.Count - 4);
                var line = run.Skip(lastWindowStartHoldingDisc).Take(4).ToList();
                return new WinningLine(line[0], line[1], line[2], line[3]);
            }
        }

        return null;
    }

    private static IEnumerable<BoardPosition> Run(Player?[] cells, PlacedDisc disc, int rowStep, int columnStep)
    {
        var next = Step(disc.Position, rowStep, columnStep);
        while (next is { } position && cells[CellIndex(position)] == disc.Player)
        {
            yield return position;
            next = Step(position, rowStep, columnStep);
        }
    }

    private static BoardPosition? Step(BoardPosition from, int rowStep, int columnStep)
    {
        var row = from.Row + rowStep;
        return row is >= 0 and < Rows && Column.TryFrom(from.Column.Value + columnStep) is { } column
            ? new BoardPosition(row, column)
            : null;
    }

    private static int CellIndex(BoardPosition position) => CellIndex(position.Row, position.Column);

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
