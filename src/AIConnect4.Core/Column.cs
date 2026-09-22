using System.Collections.Immutable;

namespace AIConnect4.Core;

/// <summary>One of the seven board columns. <see cref="Value"/> is 1 through 7, <see cref="Index"/> is <c>Value - 1</c>, and the default value is column 1.</summary>
public readonly record struct Column : IComparable<Column>
{
    public const int Count = 7;

    private readonly int _index;

    private Column(int index) => _index = index;

    public int Value => _index + 1;

    public int Index => _index;

    public static ImmutableArray<Column> All => Columns.All;

    private static class Columns
    {
        public static readonly ImmutableArray<Column> All =
            ImmutableArray.CreateRange(Enumerable.Range(0, Count).Select(index => new Column(index)));
    }

    public static Column? TryFrom(int value) => value is >= 1 and <= Count ? new Column(value - 1) : null;

    public static Column From(int value) =>
        TryFrom(value) ?? throw new ArgumentOutOfRangeException(nameof(value), value, "Column values are 1 through 7.");

    public static Column? TryParse(string? text) => int.TryParse(text?.Trim(), out var value) ? TryFrom(value) : null;

    public int CompareTo(Column other) => _index.CompareTo(other._index);

    public override string ToString() => Value.ToString();
}
