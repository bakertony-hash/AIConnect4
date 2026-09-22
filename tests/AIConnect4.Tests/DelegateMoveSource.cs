namespace AIConnect4.Tests;

internal sealed class DelegateMoveSource(Func<Decision, CancellationToken, Task<MoveReply>> answer) : IMoveSource
{
    public Task<MoveReply> GetMoveAsync(Decision decision, CancellationToken cancellationToken) => answer(decision, cancellationToken);
}
