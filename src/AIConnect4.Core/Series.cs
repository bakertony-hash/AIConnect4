using System.Diagnostics;

namespace AIConnect4.Core;

/// <summary>How many games a series plays. <see cref="GameCount"/> is at least 1. The default is 1.</summary>
public readonly record struct SeriesConfig
{
    private readonly int _extraGames;

    private SeriesConfig(int extraGames) => _extraGames = extraGames;

    public int GameCount => _extraGames + 1;

    public static SeriesConfig Default => default;

    public static SeriesConfig? TryCreate(int gameCount) => gameCount >= 1 ? new SeriesConfig(gameCount - 1) : null;

    public static SeriesConfig Of(int gameCount) =>
        TryCreate(gameCount) ?? throw new ArgumentOutOfRangeException(nameof(gameCount), gameCount, "GameCount is at least 1.");
}

/// <summary>Wins and draws across a series. Only a <see cref="GameResult"/> can move it, so an abort never scores.</summary>
public readonly record struct SeriesScore
{
    private SeriesScore(int redWins, int yellowWins, int draws)
    {
        RedWins = redWins;
        YellowWins = yellowWins;
        Draws = draws;
    }

    public int RedWins { get; }

    public int YellowWins { get; }

    public int Draws { get; }

    public int GamesFinished => RedWins + YellowWins + Draws;

    public static SeriesScore Empty => default;

    public int WinsFor(Player player) => player == Player.Red ? RedWins : YellowWins;

    public SeriesScore Record(GameResult result) => result switch
    {
        GameResult.Win { Winner: Player.Red } => new SeriesScore(RedWins + 1, YellowWins, Draws),
        GameResult.Win => new SeriesScore(RedWins, YellowWins + 1, Draws),
        GameResult.Draw => new SeriesScore(RedWins, YellowWins, Draws + 1),
        _ => throw new UnreachableException(),
    };
}

public abstract record SeriesStatus
{
    private SeriesStatus()
    {
    }

    public sealed record Ready : SeriesStatus;

    /// <param name="GameNumber">1-based index of the live game.</param>
    public sealed record Running(int GameNumber) : SeriesStatus;

    /// <param name="GameNumber">1-based index of the live game, or of the next game when paused between games.</param>
    public sealed record Paused(int GameNumber) : SeriesStatus;

    public abstract record Ended : SeriesStatus
    {
        private protected Ended()
        {
        }
    }

    public sealed record Finished : Ended;

    /// <param name="GameNumber">1-based index of the game that aborted, or of the next game when cancelled between games. That game did not score.</param>
    public sealed record Aborted(int GameNumber, AbortReason Reason) : Ended;
}
