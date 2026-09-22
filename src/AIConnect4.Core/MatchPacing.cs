namespace AIConnect4.Core;

/// <summary>
/// The runners' clocks. <see cref="Time"/> measures model calls. <see cref="WatchPause"/> runs between applied
/// moves and between games, is UI pacing, and is never measured. Tests advance a fake <see cref="Time"/> inside it.
/// </summary>
public sealed record MatchPacing(TimeProvider Time, Func<CancellationToken, Task> WatchPause)
{
    public static MatchPacing None { get; } = new(TimeProvider.System, _ => Task.CompletedTask);
}

/// <summary>Pause requests. A runner checks the gate after each applied move, so a pause never interrupts a call in flight.</summary>
public sealed class PauseGate
{
    private TaskCompletionSource? _resume;

    public bool IsPaused => Volatile.Read(ref _resume) is not null;

    public void Pause() =>
        Interlocked.CompareExchange(ref _resume, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), null);

    public void Resume() => Interlocked.Exchange(ref _resume, null)?.TrySetResult();

    public Task WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        var resume = Volatile.Read(ref _resume);
        return resume is null ? Task.CompletedTask : resume.Task.WaitAsync(cancellationToken);
    }
}
