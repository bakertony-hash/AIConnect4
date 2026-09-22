using System.Diagnostics;
using static AIConnect4.Tests.Games;

namespace AIConnect4.Tests;

public sealed class GameRunnerTests
{
    private static readonly WinningLine Column1Rows0To3 = new(At(0, 1), At(1, 1), At(2, 1), At(3, 1));

    private static readonly GameStatus.Finished RedWinsInColumn1 = new(new GameResult.Win(Player.Red, Column1Rows0To3));

    [Fact]
    public async Task Sides_alternate_from_Red_and_a_win_stops_the_game()
    {
        var red = new ScriptedMoveSource(RedVerticalWin.RedMoves());
        var yellow = new ScriptedMoveSource(RedVerticalWin.YellowMoves());
        var runner = new GameRunner(red, yellow);

        var end = await runner.PlayAsync(CancellationToken.None);

        Move[] expectedMoves =
        [
            new(Player.Red, Col(1), MoveOrigin.Model),
            new(Player.Yellow, Col(2), MoveOrigin.Model),
            new(Player.Red, Col(1), MoveOrigin.Model),
            new(Player.Yellow, Col(2), MoveOrigin.Model),
            new(Player.Red, Col(1), MoveOrigin.Model),
            new(Player.Yellow, Col(2), MoveOrigin.Model),
            new(Player.Red, Col(1), MoveOrigin.Model),
        ];
        Assert.Equal(expectedMoves, runner.Moves);
        Assert.Equal(RedWinsInColumn1, end);
        Assert.Equal(RedWinsInColumn1, runner.Status);
        Assert.Equal(Player.Yellow, runner.ToMove);
        Assert.Null(runner.Thinking);
        Assert.Equal(0, yellow.Remaining);
    }

    [Fact]
    public async Task Each_decision_names_the_side_and_the_board_it_was_asked_about()
    {
        var red = new ScriptedMoveSource(RedVerticalWin.RedMoves());
        var yellow = new ScriptedMoveSource(RedVerticalWin.YellowMoves());
        var runner = new GameRunner(red, yellow);

        await runner.PlayAsync(CancellationToken.None);

        int[] redDiscCounts = [0, 2, 4, 6];
        int[] yellowDiscCounts = [1, 3, 5];
        int[] allColumns = [1, 2, 3, 4, 5, 6, 7];
        Assert.All(red.Decisions, decision => Assert.Equal(Player.Red, decision.YouAre));
        Assert.All(yellow.Decisions, decision => Assert.Equal(Player.Yellow, decision.YouAre));
        Assert.Equal(redDiscCounts, red.Decisions.Select(decision => decision.Board.DiscCount));
        Assert.Equal(yellowDiscCounts, yellow.Decisions.Select(decision => decision.Board.DiscCount));
        Assert.All(red.Decisions, decision => Assert.Equal(allColumns, decision.Criteria.Select(column => column.Value)));
        Assert.Equal(new PlacedDisc(Player.Red, At(0, 1)), yellow.Decisions[0].LastMove);
    }

    [Fact]
    public async Task A_draw_stops_the_game_after_42_moves()
    {
        var runner = new GameRunner(new ScriptedMoveSource(Draw.RedMoves()), new ScriptedMoveSource(Draw.YellowMoves()));

        var end = await runner.PlayAsync(CancellationToken.None);

        Assert.Equal(new GameStatus.Finished(new GameResult.Draw()), end);
        Assert.Equal(42, runner.Moves.Count);
    }

    [Fact]
    public async Task A_full_column_answer_gets_one_retry_that_carries_the_failure()
    {
        var red = new ScriptedMoveSource([1, 1, 1, 1, 2, 2, 2, 2]);
        var yellow = new ScriptedMoveSource([1, 1, 1, 3, 3, 3]);
        var runner = new GameRunner(red, yellow);

        var end = await runner.PlayAsync(CancellationToken.None);

        int[] criteriaWithoutColumn1 = [2, 3, 4, 5, 6, 7];
        Assert.Equal(new Move(Player.Red, Col(2), MoveOrigin.Retry), runner.Moves[6]);
        Assert.Equal(new MoveFailure.IllegalColumn(Col(1)), red.Decisions[4].PriorFailure);
        Assert.Null(red.Decisions[3].PriorFailure);
        Assert.Equal(criteriaWithoutColumn1, red.Decisions[4].Criteria.Select(column => column.Value));
        Assert.Equal(8, red.Decisions.Count);
        Assert.Equal(13, runner.Moves.Count);
        Assert.Equal(
            new GameStatus.Finished(new GameResult.Win(Player.Red, new WinningLine(At(0, 2), At(1, 2), At(2, 2), At(3, 2)))),
            end);
    }

    [Fact]
    public async Task Two_bad_answers_abort_the_game_and_keep_the_board()
    {
        var clock = new FakeTimeProvider();
        var red = new ScriptedMoveSource(ScriptedMoveSource.Chosen([1, 1]), clock, TimeSpan.FromMilliseconds(10));
        var yellow = new ScriptedMoveSource(
            [
                new MoveReply.Chosen(Col(2)),
                new MoveReply.Failed(new MoveFailure.Empty()),
                new MoveReply.Failed(new MoveFailure.Timeout()),
                new MoveReply.Chosen(Col(7)),
                new MoveReply.Chosen(Col(7)),
            ],
            clock,
            TimeSpan.FromMilliseconds(20));
        var runner = new GameRunner(red, yellow, clock.Pacing(TimeSpan.FromMilliseconds(400)), new PauseGate());

        var end = await runner.PlayAsync(CancellationToken.None);

        Assert.Equal(new GameStatus.Aborted(new AbortReason.MoveRejected(Player.Yellow, new MoveFailure.Timeout())), end);
        Assert.Equal(end, runner.Status);
        Assert.Equal(".......\n.......\n.......\n.......\nR......\nRY.....", runner.Board.Render());
        Assert.Equal(3, runner.Moves.Count);
        MoveTiming[] expectedTimings =
        [
            new(Player.Red, TimeSpan.FromMilliseconds(10), MoveOutcome.Applied),
            new(Player.Yellow, TimeSpan.FromMilliseconds(20), MoveOutcome.Applied),
            new(Player.Red, TimeSpan.FromMilliseconds(10), MoveOutcome.Applied),
            new(Player.Yellow, TimeSpan.FromMilliseconds(40), MoveOutcome.Aborted),
        ];
        Assert.Equal(expectedTimings, runner.Timings);
        Assert.Equal(2, yellow.Remaining);
    }

    [Fact]
    public async Task A_source_that_throws_twice_aborts_with_the_exception_text()
    {
        var yellow = new DelegateMoveSource((_, _) => throw new InvalidOperationException("boom"));
        var runner = new GameRunner(new ScriptedMoveSource([1, 1]), yellow);

        var end = await runner.PlayAsync(CancellationToken.None);

        Assert.Equal(
            new GameStatus.Aborted(new AbortReason.MoveRejected(Player.Yellow, new MoveFailure.Faulted("InvalidOperationException: boom"))),
            end);
        Move[] expectedMoves = [new(Player.Red, Col(1), MoveOrigin.Model)];
        Assert.Equal(expectedMoves, runner.Moves);
    }

    [Fact]
    public async Task Decision_time_excludes_the_watch_pause_and_folds_a_retry_into_one_timing()
    {
        var clock = new FakeTimeProvider();
        var red = new ScriptedMoveSource(ScriptedMoveSource.Chosen([1, 1, 1, 1]), clock, TimeSpan.FromMilliseconds(10));
        var yellow = new ScriptedMoveSource(
            [new MoveReply.Failed(new MoveFailure.Empty()), .. ScriptedMoveSource.Chosen([2, 2, 2])],
            clock,
            TimeSpan.FromMilliseconds(20));
        var runner = new GameRunner(red, yellow, clock.Pacing(TimeSpan.FromMilliseconds(400)), new PauseGate());

        var end = await runner.PlayAsync(CancellationToken.None);

        Assert.Equal(RedWinsInColumn1, end);
        MoveTiming[] expectedTimings =
        [
            new(Player.Red, TimeSpan.FromMilliseconds(10), MoveOutcome.Applied),
            new(Player.Yellow, TimeSpan.FromMilliseconds(40), MoveOutcome.Applied),
            new(Player.Red, TimeSpan.FromMilliseconds(10), MoveOutcome.Applied),
            new(Player.Yellow, TimeSpan.FromMilliseconds(20), MoveOutcome.Applied),
            new(Player.Red, TimeSpan.FromMilliseconds(10), MoveOutcome.Applied),
            new(Player.Yellow, TimeSpan.FromMilliseconds(20), MoveOutcome.Applied),
            new(Player.Red, TimeSpan.FromMilliseconds(10), MoveOutcome.Applied),
        ];
        Assert.Equal(expectedTimings, runner.Timings);
        Assert.Equal(TimeSpan.FromMilliseconds(40), runner.TotalDecisionTime(Player.Red));
        Assert.Equal(TimeSpan.FromMilliseconds(80), runner.TotalDecisionTime(Player.Yellow));
        Assert.Equal(TimeSpan.FromMilliseconds(120), runner.CombinedDecisionTime);
        Assert.Equal(TimeSpan.FromMilliseconds(10), runner.LastDecisionTime(Player.Red));
        Assert.Equal(TimeSpan.FromMilliseconds(20), runner.LastDecisionTime(Player.Yellow));
        Assert.Equal(TimeSpan.Zero, runner.ElapsedThinking);
    }

    [Fact]
    public async Task Thinking_and_ElapsedThinking_are_live_during_a_call()
    {
        var clock = new FakeTimeProvider();
        GameRunner runner = null!;
        var seen = new List<(Player? Thinking, TimeSpan Elapsed)>();
        var yellow = new DelegateMoveSource((_, _) =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(30));
            seen.Add((runner.Thinking, runner.ElapsedThinking));
            return Task.FromResult<MoveReply>(new MoveReply.Chosen(Col(2)));
        });
        runner = new GameRunner(new ScriptedMoveSource(RedVerticalWin.RedMoves()), yellow, clock.Pacing(TimeSpan.Zero), new PauseGate());

        Assert.Null(runner.LastDecisionTime(Player.Yellow));
        await runner.PlayAsync(CancellationToken.None);

        (Player?, TimeSpan)[] expected =
        [
            (Player.Yellow, TimeSpan.FromMilliseconds(30)),
            (Player.Yellow, TimeSpan.FromMilliseconds(30)),
            (Player.Yellow, TimeSpan.FromMilliseconds(30)),
        ];
        Assert.Equal(expected, seen);
        Assert.Null(runner.Thinking);
        Assert.Equal(TimeSpan.FromMilliseconds(90), runner.TotalDecisionTime(Player.Yellow));
    }

    [Fact]
    public async Task Cancelling_the_token_during_a_call_aborts_as_Cancelled_without_throwing()
    {
        var clock = new FakeTimeProvider();
        var yellow = new DelegateMoveSource(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new UnreachableException();
        });
        var runner = new GameRunner(new ScriptedMoveSource([1, 1]), yellow, clock.Pacing(TimeSpan.Zero), new PauseGate());
        using var cancellation = new CancellationTokenSource();

        var play = runner.PlayAsync(cancellation.Token);
        cancellation.Cancel();
        var end = await play;

        Assert.Equal(new GameStatus.Aborted(new AbortReason.Cancelled()), end);
        MoveTiming[] expectedTimings =
        [
            new(Player.Red, TimeSpan.Zero, MoveOutcome.Applied),
            new(Player.Yellow, TimeSpan.Zero, MoveOutcome.Cancelled),
        ];
        Assert.Equal(expectedTimings, runner.Timings);
        Assert.Null(runner.Thinking);
    }

    [Fact]
    public async Task An_OperationCanceledException_without_cancellation_is_a_timeout_failure()
    {
        var calls = 0;
        var yellow = new DelegateMoveSource((_, _) =>
            calls++ == 0 ? throw new OperationCanceledException() : Task.FromResult<MoveReply>(new MoveReply.Chosen(Col(2))));
        var runner = new GameRunner(new ScriptedMoveSource([1, 1]), yellow);

        await runner.PlayAsync(CancellationToken.None);

        Assert.Equal(new Move(Player.Yellow, Col(2), MoveOrigin.Retry), runner.Moves[1]);
    }

    [Fact]
    public async Task Pause_lands_the_in_flight_move_first_and_Resume_plays_on()
    {
        var yellowCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pausedWith = new TaskCompletionSource<Move[]>(TaskCreationOptions.RunContinuationsAsynchronously);
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
        var runner = new GameRunner(new ScriptedMoveSource(RedVerticalWin.RedMoves()), yellow);
        runner.Changed += () =>
        {
            if (runner.Status is GameStatus.Paused)
            {
                pausedWith.TrySetResult([.. runner.Moves]);
            }
        };

        var play = runner.PlayAsync(CancellationToken.None);
        await yellowCalled.Task;
        Assert.Equal(Player.Yellow, runner.Thinking);
        runner.Pause();
        release.SetResult();
        var movesAtPause = await pausedWith.Task;

        Move[] expectedAtPause = [new(Player.Red, Col(1), MoveOrigin.Model), new(Player.Yellow, Col(2), MoveOrigin.Model)];
        Assert.Equal(expectedAtPause, movesAtPause);
        Assert.Equal(new GameStatus.Paused(), runner.Status);

        runner.Resume();
        var end = await play;

        Assert.Equal(RedWinsInColumn1, end);
        Assert.Equal(7, runner.Moves.Count);
    }

    [Fact]
    public async Task PlayAsync_twice_throws()
    {
        var runner = new GameRunner(new ScriptedMoveSource(RedVerticalWin.RedMoves()), new ScriptedMoveSource(RedVerticalWin.YellowMoves()));

        await runner.PlayAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.PlayAsync(CancellationToken.None));
    }
}
