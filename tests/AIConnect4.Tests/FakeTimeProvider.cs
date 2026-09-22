namespace AIConnect4.Tests;

internal sealed class FakeTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _timestamp;

    public void Advance(TimeSpan span) => _timestamp += span.Ticks;

    public MatchPacing Pacing(TimeSpan watchPause) => new(this, _ =>
    {
        Advance(watchPause);
        return Task.CompletedTask;
    });
}
