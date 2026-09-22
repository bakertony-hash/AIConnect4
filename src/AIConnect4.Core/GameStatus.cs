namespace AIConnect4.Core;

public abstract record GameStatus
{
    private GameStatus()
    {
    }

    public sealed record Ready : GameStatus;

    public sealed record Running : GameStatus;

    public sealed record Paused : GameStatus;

    public abstract record Ended : GameStatus
    {
        private protected Ended()
        {
        }
    }

    public sealed record Finished(GameResult Result) : Ended;

    public sealed record Aborted(AbortReason Reason) : Ended;
}

public abstract record AbortReason
{
    private AbortReason()
    {
    }

    /// <summary>The side's retry also failed. <see cref="Failure"/> is the second failure.</summary>
    public sealed record MoveRejected(Player Side, MoveFailure Failure) : AbortReason;

    public sealed record Cancelled : AbortReason;
}
