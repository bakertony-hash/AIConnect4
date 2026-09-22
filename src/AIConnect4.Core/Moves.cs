using System.Diagnostics;

namespace AIConnect4.Core;

/// <summary>A model that answers one <see cref="Decision"/> per call. Adapters close over their <c>ModelProfile</c>.</summary>
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

    /// <summary>The model answered with no column.</summary>
    public sealed record Empty : MoveFailure;

    /// <summary>The answer held no column in 1 through 7.</summary>
    public sealed record Unparseable(string Answer) : MoveFailure;

    /// <summary>The runner found the chosen column outside <see cref="Decision.Criteria"/>.</summary>
    public sealed record IllegalColumn(Column Column) : MoveFailure;

    public sealed record Timeout : MoveFailure;

    /// <summary>The call itself failed, such as a transport error or an exception from the adapter.</summary>
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
