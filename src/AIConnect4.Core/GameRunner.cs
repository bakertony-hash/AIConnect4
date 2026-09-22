namespace AIConnect4.Core;

/// <summary>
/// Headless owner of one game's status, clock, and drop. Red moves first. Each turn builds one
/// <see cref="Decision"/>, asks that side's <see cref="IMoveSource"/>, retries once with the failure in the
/// decision, and aborts the game on a second failure. Avalonia binds to it through <see cref="Changed"/>.
/// </summary>
public sealed class GameRunner
{
    private readonly IMoveSource _red;
    private readonly IMoveSource _yellow;
    private readonly MatchPacing _pacing;
    private readonly PauseGate _pause;
    private readonly List<Move> _moves = [];
    private readonly List<MoveTiming> _timings = [];

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

    /// <summary>The side whose model call is in flight, or null.</summary>
    public Player? Thinking => throw new NotImplementedException();

    /// <summary>Live elapsed time of the in-flight call. Zero when no call is in flight.</summary>
    public TimeSpan ElapsedThinking => throw new NotImplementedException();

    public TimeSpan? LastDecisionTime(Player side) => throw new NotImplementedException();

    public TimeSpan TotalDecisionTime(Player side) => throw new NotImplementedException();

    public TimeSpan CombinedDecisionTime => TotalDecisionTime(Player.Red) + TotalDecisionTime(Player.Yellow);

    /// <summary>Raised after every change to <see cref="Status"/>, <see cref="Board"/>, <see cref="Moves"/>, or <see cref="Timings"/>. Raised on the runner's thread.</summary>
    public event Action? Changed;

    public void Pause() => _pause.Pause();

    public void Resume() => _pause.Resume();

    /// <summary>Plays from <see cref="GameStatus.Ready"/> to an <see cref="GameStatus.Ended"/> status. Callable once.</summary>
    public Task<GameStatus.Ended> PlayAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    private void OnChanged() => Changed?.Invoke();
}
