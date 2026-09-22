using static AIConnect4.Tests.Games;

namespace AIConnect4.Tests;

public sealed class BoardTests
{
    [Fact]
    public void Two_drops_in_one_column_land_on_rows_0_and_1()
    {
        var board = Play(3, 3);

        Assert.Equal(Player.Red, board.Cell(0, Col(3)));
        Assert.Equal(Player.Yellow, board.Cell(1, Col(3)));
        Assert.Null(board.Cell(2, Col(3)));
        Assert.Equal(new PlacedDisc(Player.Yellow, At(1, 3)), board.LastMove);
    }

    [Fact]
    public void Empty_board_offers_columns_1_through_7()
    {
        int[] expected = [1, 2, 3, 4, 5, 6, 7];

        Assert.Equal(expected, Board.Empty.LegalColumns().Select(column => column.Value));
    }

    [Fact]
    public void Full_column_leaves_legal_columns_and_rejects_a_drop()
    {
        var board = Play(1, 1, 1, 1, 1, 1);
        int[] expected = [2, 3, 4, 5, 6, 7];

        Assert.Null(board.Result);
        Assert.Equal(expected, board.LegalColumns().Select(column => column.Value));
        Assert.Null(board.Drop(Player.Red, Col(1)));
    }

    [Fact]
    public void Horizontal_four_wins_with_the_line_ordered_from_the_left()
    {
        var board = Play(1, 1, 2, 2, 4, 4, 3);

        Assert.Equal(new GameResult.Win(Player.Red, new WinningLine(At(0, 1), At(0, 2), At(0, 3), At(0, 4))), board.Result);
    }

    [Fact]
    public void Vertical_four_wins_with_the_line_ordered_from_the_bottom()
    {
        var board = Play(RedVerticalWin);

        Assert.Equal(new GameResult.Win(Player.Red, new WinningLine(At(0, 1), At(1, 1), At(2, 1), At(3, 1))), board.Result);
    }

    [Fact]
    public void Rising_diagonal_four_wins()
    {
        var board = Play(1, 2, 2, 3, 3, 4, 3, 4, 7, 4, 4);

        Assert.Equal(new GameResult.Win(Player.Red, new WinningLine(At(0, 1), At(1, 2), At(2, 3), At(3, 4))), board.Result);
    }

    [Fact]
    public void Falling_diagonal_four_wins_with_the_line_ordered_from_the_bottom()
    {
        var board = Play(7, 6, 6, 5, 5, 4, 5, 4, 1, 4, 4);

        Assert.Equal(new GameResult.Win(Player.Red, new WinningLine(At(0, 7), At(1, 6), At(2, 5), At(3, 4))), board.Result);
    }

    [Fact]
    public void Yellow_can_win_too()
    {
        var board = Play(YellowVerticalWin);

        Assert.Equal(new GameResult.Win(Player.Yellow, new WinningLine(At(0, 2), At(1, 2), At(2, 2), At(3, 2))), board.Result);
    }

    [Fact]
    public void Full_board_without_four_in_a_row_is_a_draw()
    {
        var board = Play(Draw);

        Assert.Equal(42, board.DiscCount);
        Assert.Equal(new GameResult.Draw(), board.Result);
        Assert.Empty(board.LegalColumns());
        Assert.Null(board.Drop(Player.Red, Col(1)));
    }

    [Fact]
    public void Won_board_accepts_no_more_drops()
    {
        var board = Play(RedVerticalWin);

        Assert.Empty(board.LegalColumns());
        Assert.Null(board.Drop(Player.Yellow, Col(3)));
    }

    [Fact]
    public void Render_prints_the_top_row_first()
    {
        var board = Play(4, 4, 3);

        Assert.Equal(".......\n.......\n.......\n.......\n...Y...\n..RR...", board.Render());
    }
}
