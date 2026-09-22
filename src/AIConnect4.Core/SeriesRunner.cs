using System.Diagnostics;

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
    private readonly List<GameRunner> _games = [];

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
    public GameRunner? CurrentGame => _games.Count == 0 ? null : _games[^1];

    public int GamesRemaining => Config.GameCount - Score.GamesFinished;

    public TimeSpan TotalDecisionTime(Player side) =>
        _games.Aggregate(TimeSpan.Zero, (total, game) => total + game.TotalDecisionTime(side));

    public TimeSpan CombinedDecisionTime => TotalDecisionTime(Player.Red) + TotalDecisionTime(Player.Yellow);

    /// <summary>Raised after every change to <see cref="Status"/>, <see cref="Score"/>, or <see cref="CurrentGame"/>, and forwarded from the live game.</summary>
    public event Action? Changed;

    public void Pause() => _pause.Pause();

    public void Resume() => _pause.Resume();

    /// <summary>Plays from <see cref="SeriesStatus.Ready"/> to a <see cref="SeriesStatus.Ended"/> status. Callable once.</summary>
    public async Task<SeriesStatus.Ended> PlayAsync(CancellationToken cancellationToken)
    {
        if (Status is not SeriesStatus.Ready)
        {
            throw new InvalidOperationException("PlayAsync runs once, from Ready.");
        }

        for (var gameNumber = 1; gameNumber <= Config.GameCount; gameNumber++)
        {
            if (gameNumber > 1)
            {
                try
                {
                    await _pacing.WatchPause(cancellationToken);
                    if (_pause.IsPaused)
                    {
                        SetStatus(new SeriesStatus.Paused(gameNumber));
                        await _pause.WaitWhilePausedAsync(cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return SetStatus(new SeriesStatus.Aborted(gameNumber, new AbortReason.Cancelled()));
                }
            }

            var game = new GameRunner(_red, _yellow, _pacing, _pause);
            var number = gameNumber;
            game.Changed += () => MirrorGamePause(game, number);
            _games.Add(game);
            SetStatus(new SeriesStatus.Running(gameNumber));

            switch (await game.PlayAsync(cancellationToken))
            {
                case GameStatus.Finished finished:
                    Score = Score.Record(finished.Result);
                    OnChanged();
                    break;
                case GameStatus.Aborted aborted:
                    return SetStatus(new SeriesStatus.Aborted(gameNumber, aborted.Reason));
                default:
                    throw new UnreachableException();
            }
        }

        return SetStatus(new SeriesStatus.Finished());
    }

    private void MirrorGamePause(GameRunner game, int gameNumber)
    {
        Status = (game.Status, Status) switch
        {
            (GameStatus.Paused, SeriesStatus.Running) => new SeriesStatus.Paused(gameNumber),
            (GameStatus.Running, SeriesStatus.Paused) => new SeriesStatus.Running(gameNumber),
            _ => Status,
        };
        OnChanged();
    }

    private T SetStatus<T>(T status)
        where T : SeriesStatus
    {
        Status = status;
        OnChanged();
        return status;
    }

    private void OnChanged() => Changed?.Invoke();
}
