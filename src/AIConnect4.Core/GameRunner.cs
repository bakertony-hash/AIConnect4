using System.Diagnostics;

namespace AIConnect4.Core;

/// <summary>
/// Headless owner of one game's status, clock, and drop. Red moves first. Each turn builds one
/// <see cref="Decision"/>, asks that side's <see cref="IMoveSource"/>, retries once with the failure in the
/// decision, and aborts the game on a second failure.
/// </summary>
public sealed class GameRunner
{
    private readonly IMoveSource _red;
    private readonly IMoveSource _yellow;
    private readonly MatchPacing _pacing;
    private readonly PauseGate _pause;
    private readonly List<Move> _moves = [];
    private readonly List<MoveTiming> _timings = [];
    private (Player Side, long StartedAt)? _inFlight;

    public GameRunner(IMoveSource red, IMoveSource yellow)
        : this(red, yellow, MatchPacing.None, new PauseGate())
    {
    }

    public GameRunner(IMoveSource red, IMoveSource yellow, MatchPacing pacing, PauseGate pause)
    {
        _red = red;
        _yellow = yellow;
        _pacing = pacing;
        _pause = pause;
    }

    public Board Board { get; private set; } = Board.Empty;

    public Player ToMove { get; private set; } = Player.Red;

    public GameStatus Status { get; private set; } = new GameStatus.Ready();

    public IReadOnlyList<Move> Moves => _moves;

    public IReadOnlyList<MoveTiming> Timings => _timings;

    public Player? Thinking => _inFlight?.Side;

    /// <summary>Live elapsed time of the in-flight call. Zero when no call is in flight.</summary>
    public TimeSpan ElapsedThinking => _inFlight is { } call ? _pacing.Time.GetElapsedTime(call.StartedAt) : TimeSpan.Zero;

    public TimeSpan? LastDecisionTime(Player side) => _timings.LastOrDefault(timing => timing.Player == side)?.Duration;

    public TimeSpan TotalDecisionTime(Player side) =>
        _timings.Where(timing => timing.Player == side).Aggregate(TimeSpan.Zero, (total, timing) => total + timing.Duration);

    public TimeSpan CombinedDecisionTime => TotalDecisionTime(Player.Red) + TotalDecisionTime(Player.Yellow);

    /// <summary>Raised after every change to <see cref="Status"/>, <see cref="Board"/>, <see cref="Moves"/>, <see cref="Timings"/>, or <see cref="Thinking"/>. Raised on the runner's thread.</summary>
    public event Action? Changed;

    public void Pause() => _pause.Pause();

    public void Resume() => _pause.Resume();

    /// <summary>Plays from <see cref="GameStatus.Ready"/> to an <see cref="GameStatus.Ended"/> status. Callable once.</summary>
    public async Task<GameStatus.Ended> PlayAsync(CancellationToken cancellationToken)
    {
        if (Status is not GameStatus.Ready)
        {
            throw new InvalidOperationException("PlayAsync runs once, from Ready.");
        }

        SetStatus(new GameStatus.Running());
        while (true)
        {
            var side = ToMove;
            var source = side == Player.Red ? _red : _yellow;
            var decision = Decision.For(Board, side);
            _inFlight = (side, _pacing.Time.GetTimestamp());
            OnChanged();

            MoveReply reply;
            var origin = MoveOrigin.Model;
            try
            {
                reply = await JudgeAsync(source, decision, cancellationToken);
                if (reply is MoveReply.Failed first)
                {
                    origin = MoveOrigin.Retry;
                    reply = await JudgeAsync(source, decision.Retry(first.Failure), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                StopClock(MoveOutcome.Cancelled);
                return SetStatus(new GameStatus.Aborted(new AbortReason.Cancelled()));
            }

            switch (reply)
            {
                case MoveReply.Chosen chosen:
                    StopClock(MoveOutcome.Applied);
                    Board = Board.Drop(side, chosen.Column) ?? throw new UnreachableException();
                    _moves.Add(new Move(side, chosen.Column, origin));
                    ToMove = side.Opponent();
                    OnChanged();
                    break;
                case MoveReply.Failed second:
                    StopClock(MoveOutcome.Aborted);
                    return SetStatus(new GameStatus.Aborted(new AbortReason.MoveRejected(side, second.Failure)));
                default:
                    throw new UnreachableException();
            }

            if (Board.Result is { } result)
            {
                return SetStatus(new GameStatus.Finished(result));
            }

            try
            {
                await _pacing.WatchPause(cancellationToken);
                if (_pause.IsPaused)
                {
                    SetStatus(new GameStatus.Paused());
                    await _pause.WaitWhilePausedAsync(cancellationToken);
                    SetStatus(new GameStatus.Running());
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return SetStatus(new GameStatus.Aborted(new AbortReason.Cancelled()));
            }
        }
    }

    private static async Task<MoveReply> JudgeAsync(IMoveSource source, Decision decision, CancellationToken cancellationToken)
    {
        try
        {
            return await source.GetMoveAsync(decision, cancellationToken) switch
            {
                MoveReply.Chosen chosen when decision.Allows(chosen.Column) => chosen,
                MoveReply.Chosen chosen => new MoveReply.Failed(new MoveFailure.IllegalColumn(chosen.Column)),
                MoveReply.Failed failed => failed,
                _ => throw new UnreachableException(),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new MoveReply.Failed(new MoveFailure.Timeout());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new MoveReply.Failed(new MoveFailure.Faulted($"{exception.GetType().Name}: {exception.Message}"));
        }
    }

    private void StopClock(MoveOutcome outcome)
    {
        var (side, startedAt) = _inFlight ?? throw new UnreachableException();
        _timings.Add(new MoveTiming(side, _pacing.Time.GetElapsedTime(startedAt), outcome));
        _inFlight = null;
    }

    private T SetStatus<T>(T status)
        where T : GameStatus
    {
        Status = status;
        OnChanged();
        return status;
    }

    private void OnChanged() => Changed?.Invoke();
}
