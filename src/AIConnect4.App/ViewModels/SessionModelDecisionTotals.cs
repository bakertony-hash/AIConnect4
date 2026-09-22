namespace AIConnect4.App.ViewModels;

/// <summary>
/// Session-scoped cumulative decision time per model id. Survives New Series; clears only with the process.
/// </summary>
public sealed class SessionModelDecisionTotals
{
    private readonly Dictionary<string, Entry> _byModelId = new(StringComparer.Ordinal);

    public void Add(string modelId, string displayName, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (_byModelId.TryGetValue(modelId, out var existing))
        {
            _byModelId[modelId] = existing with
            {
                DisplayName = displayName,
                Total = existing.Total + duration,
            };
            return;
        }

        _byModelId[modelId] = new Entry(displayName, duration);
    }

    public IReadOnlyList<ModelDecisionTotal> Snapshot() =>
        _byModelId
            .OrderBy(pair => pair.Value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new ModelDecisionTotal(pair.Key, pair.Value.DisplayName, pair.Value.Total))
            .ToList();

    private sealed record Entry(string DisplayName, TimeSpan Total);
}

public sealed record ModelDecisionTotal(string ModelId, string DisplayName, TimeSpan Total);
