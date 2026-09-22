namespace AIConnect4.Tests;

public class DecisionPromptTests
{
    [Fact]
    public void Criterion_marks_a_forced_win_column()
    {
        var decision = Decision.For(Games.Play(1, 7, 2, 7, 3, 6), Player.Red);

        var win = DecisionPrompt.Criterion(decision, Column.From(4));
        Assert.Contains("[[COL:4]]", win);
        Assert.Contains("Playing here wins immediately.", win);
        Assert.DoesNotContain("blocks an immediate opponent win", win);

        Assert.DoesNotContain("wins immediately", DecisionPrompt.Criterion(decision, Column.From(5)));
    }

    [Fact]
    public void Criterion_marks_a_forced_block_column()
    {
        var decision = Decision.For(Games.Play(7, 1, 7, 1, 6, 1), Player.Red);

        var block = DecisionPrompt.Criterion(decision, Column.From(1));
        Assert.Contains("[[COL:1]]", block);
        Assert.Contains("Playing here blocks an immediate opponent win.", block);
        Assert.DoesNotContain("wins immediately", block);

        Assert.DoesNotContain("blocks an immediate opponent win", DecisionPrompt.Criterion(decision, Column.From(4)));
    }

    [Fact]
    public void Empty_board_criteria_are_all_distinct()
    {
        var decision = Decision.For(Board.Empty, Player.Red);

        var texts = decision.Criteria.Select(column => DecisionPrompt.Criterion(decision, column)).ToList();

        Assert.Equal(7, texts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(texts, text => Assert.DoesNotContain("wins immediately", text));
        Assert.All(texts, text => Assert.DoesNotContain("blocks an immediate opponent win", text));
        Assert.StartsWith("[[COL:1]]", texts[0]);
        Assert.StartsWith("[[COL:7]]", texts[6]);
    }

    [Fact]
    public void Choice_instructions_and_play_reminder_have_no_centre_bias()
    {
        var decision = Decision.For(Board.Empty, Player.Red);

        Assert.DoesNotContain("centre", DecisionPrompt.ChoiceInstructions(decision), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("center", DecisionPrompt.ChoiceInstructions(decision), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("centre", DecisionPrompt.PlayReminder(decision), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("center", DecisionPrompt.PlayReminder(decision), StringComparison.OrdinalIgnoreCase);
    }
}
