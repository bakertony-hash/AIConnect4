namespace AIConnect4.Tests;

internal static class Games
{
    public static readonly int[] RedVerticalWin = [1, 2, 1, 2, 1, 2, 1];

    public static readonly int[] YellowVerticalWin = [1, 2, 1, 2, 1, 2, 7, 2];

    public static readonly int[] Draw =
    [
        1, 3, 3, 1, 1, 3, 3, 1, 1, 3, 3, 1,
        2, 4, 4, 2, 2, 4, 4, 2, 2, 4, 4, 2,
        5, 7, 7, 5, 5, 7, 7, 5, 5, 7, 7, 5,
        6, 6, 6, 6, 6, 6,
    ];

    public static IEnumerable<int> RedMoves(this int[] game) => game.Where((_, index) => index % 2 == 0);

    public static IEnumerable<int> YellowMoves(this int[] game) => game.Where((_, index) => index % 2 == 1);

    public static Column Col(int value) => Column.From(value);

    public static BoardPosition At(int row, int column) => new(row, Column.From(column));

    public static Board Play(params int[] columns)
    {
        var board = Board.Empty;
        var player = Player.Red;
        foreach (var column in columns)
        {
            board = board.Drop(player, Column.From(column)) ?? throw new InvalidOperationException($"Column {column} is not legal here.");
            player = player.Opponent();
        }

        return board;
    }
}
