namespace AIConnect4.Tests;

internal sealed class ScriptedMoveSource : IMoveSource
{
    private readonly Queue<MoveReply> _script;
    private readonly FakeTimeProvider? _clock;
    private readonly TimeSpan _thinkTime;

    public ScriptedMoveSource(IEnumerable<int> columns)
        : this(Chosen(columns))
    {
    }

    public ScriptedMoveSource(IEnumerable<MoveReply> script, FakeTimeProvider? clock = null, TimeSpan thinkTime = default)
    {
        _script = new Queue<MoveReply>(script);
        _clock = clock;
        _thinkTime = thinkTime;
    }

    public List<Decision> Decisions { get; } = [];

    public int Remaining => _script.Count;

    public static IEnumerable<MoveReply> Chosen(IEnumerable<int> columns) =>
        columns.Select(column => (MoveReply)new MoveReply.Chosen(Column.From(column)));

    public Task<MoveReply> GetMoveAsync(Decision decision, CancellationToken cancellationToken)
    {
        Decisions.Add(decision);
        if (_script.Count == 0)
        {
            throw new InvalidOperationException($"The {decision.YouAre} script ran out after {Decisions.Count - 1} answers.");
        }

        _clock?.Advance(_thinkTime);
        return Task.FromResult(_script.Dequeue());
    }
}
