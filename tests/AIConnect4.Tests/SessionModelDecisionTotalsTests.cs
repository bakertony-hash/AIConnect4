using AIConnect4.App.ViewModels;

namespace AIConnect4.Tests;

public sealed class SessionModelDecisionTotalsTests
{
    [Fact]
    public void Add_accumulates_by_model_id_across_calls()
    {
        var totals = new SessionModelDecisionTotals();

        totals.Add("openai/gpt-6-luna", "GPT-6 Luna", TimeSpan.FromSeconds(1.5));
        totals.Add("openai/gpt-6-luna", "GPT-6 Luna", TimeSpan.FromSeconds(2.5));
        totals.Add("typesafe/jev-1.13", "Jev 1.13", TimeSpan.FromSeconds(10));

        var snapshot = totals.Snapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(new ModelDecisionTotal("openai/gpt-6-luna", "GPT-6 Luna", TimeSpan.FromSeconds(4)), snapshot[0]);
        Assert.Equal(new ModelDecisionTotal("typesafe/jev-1.13", "Jev 1.13", TimeSpan.FromSeconds(10)), snapshot[1]);
    }

    [Fact]
    public void Same_model_id_from_both_sides_shares_one_bucket()
    {
        var totals = new SessionModelDecisionTotals();

        totals.Add("openai/gpt-6-luna", "GPT-6 Luna", TimeSpan.FromSeconds(3));
        totals.Add("openai/gpt-6-luna", "GPT-6 Luna", TimeSpan.FromSeconds(7));

        var snapshot = totals.Snapshot();
        Assert.Single(snapshot);
        Assert.Equal(TimeSpan.FromSeconds(10), snapshot[0].Total);
        Assert.Equal("openai/gpt-6-luna", snapshot[0].ModelId);
    }

    [Fact]
    public void Switching_models_creates_a_new_bucket()
    {
        var totals = new SessionModelDecisionTotals();

        totals.Add("typesafe/jev-1.13", "Jev 1.13", TimeSpan.FromSeconds(5));
        totals.Add("~typesafe/jev-latest", "Jev Latest", TimeSpan.FromSeconds(8));

        var snapshot = totals.Snapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Contains(snapshot, row => row is { ModelId: "typesafe/jev-1.13", Total.TotalSeconds: 5 });
        Assert.Contains(snapshot, row => row is { ModelId: "~typesafe/jev-latest", Total.TotalSeconds: 8 });
    }

    [Fact]
    public void Later_display_name_for_same_id_updates_the_label()
    {
        var totals = new SessionModelDecisionTotals();

        totals.Add("custom/id", "Old Label", TimeSpan.FromSeconds(1));
        totals.Add("custom/id", "New Label", TimeSpan.FromSeconds(2));

        var snapshot = totals.Snapshot();
        Assert.Single(snapshot);
        Assert.Equal("New Label", snapshot[0].DisplayName);
        Assert.Equal(TimeSpan.FromSeconds(3), snapshot[0].Total);
    }

    [Fact]
    public void Snapshot_orders_by_display_name()
    {
        var totals = new SessionModelDecisionTotals();

        totals.Add("b", "Zebra", TimeSpan.FromSeconds(1));
        totals.Add("a", "Alpha", TimeSpan.FromSeconds(1));

        Assert.Equal(["Alpha", "Zebra"], totals.Snapshot().Select(row => row.DisplayName).ToArray());
    }
}
