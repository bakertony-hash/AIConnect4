namespace AIConnect4.Core;

/// <summary>
/// Headless owner of a series. Plays <see cref="SeriesConfig.GameCount"/> games with the same two sources,
/// starts the next <see cref="GameRunner"/> after a finish, and stops the series on an abort. Only a finished game moves <see cref="Score"/>.
/// </summary>
public sealed class SeriesRunner
{
    private readonly IMoveSource _red;
    private readonly IMoveSource _yellow;
    private readonly MatchPacing _pacing;
    private readonly PauseGate _pause = new();

    public SeriesRunner(IMoveSource red, IMoveSource yellow, SeriesConfig config)
        : this(red, yellow, config, MatchPacing.None)
    {
    }

    public SeriesRunner(IMoveSource red, IMoveSource yellow, SeriesConfig config, MatchPacing pacing)
    {
        _red = red;
        _yellow = yellow;
        Config = config;
        _pacing = pacing;
    }

    public SeriesConfig Config { get; }

    public SeriesScore Score { get; private set; } = SeriesScore.Empty;

    public SeriesStatus Status { get; private set; } = new SeriesStatus.Ready();

    /// <summary>The live or most recent game. Null before the first game starts.</summary>
    public GameRunner? CurrentGame { get; private set; }

    public int GamesRemaining => Config.GameCount - Score.GamesFinished;

    /// <summary>Decision time for <paramref name="side"/> summed across every game in the series, including the live one.</summary>
    public TimeSpan TotalDecisionTime(Player side) => throw new NotImplementedException();

    public TimeSpan CombinedDecisionTime => TotalDecisionTime(Player.Red) + TotalDecisionTime(Player.Yellow);

    /// <summary>Raised after every change to <see cref="Status"/>, <see cref="Score"/>, or <see cref="CurrentGame"/>, and forwarded from the live game.</summary>
    public event Action? Changed;

    public void Pause() => _pause.Pause();

    public void Resume() => _pause.Resume();

    /// <summary>Plays from <see cref="SeriesStatus.Ready"/> to <see cref="SeriesStatus.Finished"/> or <see cref="SeriesStatus.Aborted"/>. Callable once.</summary>
    public Task<SeriesStatus> PlayAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    private void OnChanged() => Changed?.Invoke();
}
