using static AIConnect4.Tests.Games;

namespace AIConnect4.Tests;

public sealed class SeriesRunnerTests
{
    [Fact]
    public async Task A_series_of_three_scores_each_finished_game_and_runs_the_game_numbers_in_order()
    {
        var red = new ScriptedMoveSource([.. RedVerticalWin.RedMoves(), .. YellowVerticalWin.RedMoves(), .. Draw.RedMoves()]);
        var yellow = new ScriptedMoveSource([.. RedVerticalWin.YellowMoves(), .. YellowVerticalWin.YellowMoves(), .. Draw.YellowMoves()]);
        var series = new SeriesRunner(red, yellow, SeriesConfig.Of(3));
        var statuses = new List<SeriesStatus>();
        series.Changed += () => statuses.Add(series.Status);

        var end = await series.PlayAsync(CancellationToken.None);

        Assert.Equal(new SeriesStatus.Finished(), end);
        Assert.Equal((1, 1, 1), (series.Score.RedWins, series.Score.YellowWins, series.Score.Draws));
        Assert.Equal(0, series.GamesRemaining);
        SeriesStatus[] expectedStatuses = [new SeriesStatus.Running(1), new SeriesStatus.Running(2), new SeriesStatus.Running(3), new SeriesStatus.Finished()];
        Assert.Equal(expectedStatuses, statuses.Distinct());
        Assert.Equal(42, series.CurrentGame?.Moves.Count);
        Assert.Equal(0, red.Remaining);
        Assert.Equal(0, yellow.Remaining);
    }

    [Fact]
    public async Task An_aborted_second_game_stops_the_series_without_scoring_or_starting_the_third()
    {
        var red = new ScriptedMoveSource(
        [
            .. ScriptedMoveSource.Chosen(RedVerticalWin.RedMoves()),
            new MoveReply.Chosen(Col(1)),
            new MoveReply.Failed(new MoveFailure.Empty()),
            new MoveReply.Failed(new MoveFailure.Unparseable("nope")),
            .. ScriptedMoveSource.Chosen(RedVerticalWin.RedMoves()),
        ]);
        var yellow = new ScriptedMoveSource([.. RedVerticalWin.YellowMoves(), 2, .. RedVerticalWin.YellowMoves()]);
        var series = new SeriesRunner(red, yellow, SeriesConfig.Of(3));

        var end = await series.PlayAsync(CancellationToken.None);

        Assert.Equal(
            new SeriesStatus.Aborted(2, new AbortReason.MoveRejected(Player.Red, new MoveFailure.Unparseable("nope"))),
            end);
        Assert.Equal(end, series.Status);
        Assert.Equal((1, 0, 0), (series.Score.RedWins, series.Score.YellowWins, series.Score.Draws));
        Assert.Equal(2, series.GamesRemaining);
        Assert.Equal(4, red.Remaining);
        Assert.Equal(3, yellow.Remaining);
        Assert.Equal(2, series.CurrentGame?.Moves.Count);
    }

    [Fact]
    public async Task Series_totals_add_decision_time_across_games_and_skip_the_watch_pauses()
    {
        var clock = new FakeTimeProvider();
        var red = new ScriptedMoveSource(
            ScriptedMoveSource.Chosen([.. RedVerticalWin.RedMoves(), .. YellowVerticalWin.RedMoves()]),
            clock,
            TimeSpan.FromMilliseconds(10));
        var yellow = new ScriptedMoveSource(
            ScriptedMoveSource.Chosen([.. RedVerticalWin.YellowMoves(), .. YellowVerticalWin.YellowMoves()]),
            clock,
            TimeSpan.FromMilliseconds(20));
        var series = new SeriesRunner(red, yellow, SeriesConfig.Of(2), clock.Pacing(TimeSpan.FromMilliseconds(400)));

        var end = await series.PlayAsync(CancellationToken.None);

        Assert.Equal(new SeriesStatus.Finished(), end);
        Assert.Equal(TimeSpan.FromMilliseconds(80), series.TotalDecisionTime(Player.Red));
        Assert.Equal(TimeSpan.FromMilliseconds(140), series.TotalDecisionTime(Player.Yellow));
        Assert.Equal(TimeSpan.FromMilliseconds(220), series.CombinedDecisionTime);
        Assert.Equal(TimeSpan.FromMilliseconds(40), series.CurrentGame?.TotalDecisionTime(Player.Red));
    }

    [Fact]
    public async Task Pausing_the_live_game_pauses_the_series_and_Resume_plays_the_rest()
    {
        var yellowCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paused = new TaskCompletionSource<SeriesStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var yellowCalls = 0;
        var yellow = new DelegateMoveSource(async (_, _) =>
        {
            if (yellowCalls++ == 0)
            {
                yellowCalled.SetResult();
                await release.Task;
            }

            return new MoveReply.Chosen(Col(2));
        });
        var red = new ScriptedMoveSource([.. RedVerticalWin.RedMoves(), .. RedVerticalWin.RedMoves()]);
        var series = new SeriesRunner(red, yellow, SeriesConfig.Of(2));
        var statuses = new List<SeriesStatus>();
        series.Changed += () =>
        {
            statuses.Add(series.Status);
            if (series.Status is SeriesStatus.Paused)
            {
                paused.TrySetResult(series.Status);
            }
        };

        var play = series.PlayAsync(CancellationToken.None);
        await yellowCalled.Task;
        series.Pause();
        release.SetResult();

        Assert.Equal(new SeriesStatus.Paused(1), await paused.Task);
        Assert.Equal(new GameStatus.Paused(), series.CurrentGame?.Status);

        series.Resume();
        var end = await play;

        Assert.Equal(new SeriesStatus.Finished(), end);
        Assert.Equal((2, 0, 0), (series.Score.RedWins, series.Score.YellowWins, series.Score.Draws));
        SeriesStatus[] expectedStatuses =
        [
            new SeriesStatus.Running(1),
            new SeriesStatus.Paused(1),
            new SeriesStatus.Running(1),
            new SeriesStatus.Running(2),
            new SeriesStatus.Finished(),
        ];
        Assert.Equal(expectedStatuses, statuses.Where((status, index) => index == 0 || status != statuses[index - 1]));
    }

    [Fact]
    public async Task The_default_config_plays_exactly_one_game()
    {
        var red = new ScriptedMoveSource(RedVerticalWin.RedMoves());
        var yellow = new ScriptedMoveSource(RedVerticalWin.YellowMoves());
        var series = new SeriesRunner(red, yellow, SeriesConfig.Default);

        var end = await series.PlayAsync(CancellationToken.None);

        Assert.Equal(new SeriesStatus.Finished(), end);
        Assert.Equal((1, 0, 0), (series.Score.RedWins, series.Score.YellowWins, series.Score.Draws));
        Assert.Equal(0, red.Remaining);
        Assert.Equal(0, yellow.Remaining);
    }

    [Fact]
    public async Task PlayAsync_twice_throws()
    {
        var series = new SeriesRunner(new ScriptedMoveSource(RedVerticalWin.RedMoves()), new ScriptedMoveSource(RedVerticalWin.YellowMoves()), SeriesConfig.Default);

        await series.PlayAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => series.PlayAsync(CancellationToken.None));
    }
}
