using static AIConnect4.Tests.Games;

namespace AIConnect4.Tests;

public sealed class SeriesTypesTests
{
    [Fact]
    public void SeriesConfig_needs_at_least_one_game()
    {
        Assert.Null(SeriesConfig.TryCreate(0));
        Assert.Equal(1, SeriesConfig.TryCreate(1)?.GameCount);
        Assert.Equal(1, SeriesConfig.Default.GameCount);
        Assert.Equal(3, SeriesConfig.Of(3).GameCount);
    }

    [Fact]
    public void SeriesScore_counts_each_result_once()
    {
        var line = new WinningLine(At(0, 1), At(1, 1), At(2, 1), At(3, 1));

        var score = SeriesScore.Empty
            .Record(new GameResult.Win(Player.Red, line))
            .Record(new GameResult.Draw())
            .Record(new GameResult.Win(Player.Yellow, line))
            .Record(new GameResult.Win(Player.Yellow, line));

        Assert.Equal(1, score.RedWins);
        Assert.Equal(2, score.YellowWins);
        Assert.Equal(1, score.Draws);
        Assert.Equal(4, score.GamesFinished);
        Assert.Equal(2, score.WinsFor(Player.Yellow));
        Assert.Equal(1, score.WinsFor(Player.Red));
    }
}
