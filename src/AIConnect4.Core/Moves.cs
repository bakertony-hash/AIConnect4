using System.Diagnostics;

namespace AIConnect4.Core;

public interface IMoveSource
{
    Task<MoveReply> GetMoveAsync(Decision decision, CancellationToken cancellationToken);
}

public abstract record MoveReply
{
    private MoveReply()
    {
    }

    public sealed record Chosen(Column Column, string? Reason = null) : MoveReply;

    public sealed record Failed(MoveFailure Failure) : MoveReply;
}

public abstract record MoveFailure
{
    private MoveFailure()
    {
    }

    public sealed record Empty : MoveFailure;

    public sealed record Unparseable(string Answer) : MoveFailure;

    public sealed record IllegalColumn(Column Column) : MoveFailure;

    public sealed record Timeout : MoveFailure;

    public sealed record Faulted(string Message) : MoveFailure;

    /// <summary>One sentence an adapter can place in the retry prompt.</summary>
    public string Describe() => this switch
    {
        Empty => "Your previous answer was empty.",
        Unparseable failure => $"Your previous answer \"{failure.Answer}\" held no column in 1 through 7.",
        IllegalColumn failure => $"Column {failure.Column} is full. Pick a legal column.",
        Timeout => "Your previous answer timed out.",
        Faulted failure => $"Your previous call failed: {failure.Message}",
        _ => throw new UnreachableException(),
    };
}

public enum MoveOrigin
{
    Model,
    Retry,
}

public sealed record Move(Player Player, Column Column, MoveOrigin Origin);

public enum MoveOutcome
{
    Applied,
    Aborted,
    Cancelled,
}

/// <summary>The measured span of one side's model call, including its single retry.</summary>
public sealed record MoveTiming(Player Player, TimeSpan Duration, MoveOutcome Outcome);
