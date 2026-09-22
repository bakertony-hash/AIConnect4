using AIConnect4.Core;

namespace AIConnect4.App.OpenRouter;

/// <summary>The last step of both adapters. A parsed integer becomes a legal <see cref="Column"/> or a typed failure. Never a substitute column.</summary>
public static class ColumnAnswer
{
    public static MoveReply Resolve(int value, string answer, Decision decision, string? reason = null) =>
        Column.TryFrom(value) is not { } column ? new MoveReply.Failed(new MoveFailure.Unparseable(answer))
        : !decision.Allows(column) ? new MoveReply.Failed(new MoveFailure.IllegalColumn(column))
        : new MoveReply.Chosen(column, reason);
}
