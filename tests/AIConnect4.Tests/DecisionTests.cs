using static AIConnect4.Tests.Games;

namespace AIConnect4.Tests;

public sealed class DecisionTests
{
    [Fact]
    public void Criteria_are_the_legal_columns_in_order()
    {
        var decision = Decision.For(Play(1, 1, 1, 1, 1, 1), Player.Red);
        int[] expected = [2, 3, 4, 5, 6, 7];

        Assert.Equal(expected, decision.Criteria.Select(column => column.Value));
        Assert.Equal(Player.Red, decision.YouAre);
        Assert.Null(decision.PriorFailure);
        Assert.False(decision.Allows(Col(1)));
        Assert.True(decision.Allows(Col(2)));
    }

    [Fact]
    public void For_throws_on_a_finished_board()
    {
        var board = Play(RedVerticalWin);

        Assert.Throws<InvalidOperationException>(() => Decision.For(board, Player.Yellow));
    }

    [Fact]
    public void Retry_carries_the_failure_and_keeps_the_criteria()
    {
        var failure = new MoveFailure.IllegalColumn(Col(3));
        int[] expected = [1, 2, 3, 4, 5, 6, 7];

        var retry = Decision.For(Board.Empty, Player.Yellow).Retry(failure);

        Assert.Equal(failure, retry.PriorFailure);
        Assert.Equal(expected, retry.Criteria.Select(column => column.Value));
        Assert.Equal(Player.Yellow, retry.YouAre);
    }

    [Fact]
    public void Grid_and_LastMove_come_from_the_board()
    {
        var decision = Decision.For(Play(4, 4, 3), Player.Yellow);

        Assert.Equal(".......\n.......\n.......\n.......\n...Y...\n..RR...", decision.Grid);
        Assert.Equal(new PlacedDisc(Player.Red, At(0, 3)), decision.LastMove);
    }
}
