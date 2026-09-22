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
    public void SystemOne_empty_board_criteria_are_identical_and_hide_column_identity()
    {
        var decision = Decision.For(Board.Empty, Player.Red);

        var texts = decision.Criteria.Select(column => DecisionPrompt.CriterionForSystemOne(decision, column)).ToList();

        Assert.Equal(7, texts.Count);
        Assert.Equal("Empty. A disc drops to row 0.", Assert.Single(texts.Distinct(StringComparer.Ordinal)));
        Assert.All(texts, text =>
        {
            Assert.DoesNotContain("[[COL:", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Column ", text, StringComparison.Ordinal);
            Assert.DoesNotContain("wins immediately", text);
            Assert.DoesNotContain("blocks an immediate opponent win", text);
        });
    }

    [Fact]
    public void SystemOne_criterion_keeps_stack_facts_and_win_block_without_column_markers()
    {
        var stackDecision = Decision.For(Games.Play(4, 4, 4), Player.Yellow);
        Assert.Equal(
            "3 disc(s) from the bottom: R-Y-R. Next disc lands on row 3.",
            DecisionPrompt.CriterionForSystemOne(stackDecision, Column.From(4)));
        Assert.Equal(
            "Empty. A disc drops to row 0.",
            DecisionPrompt.CriterionForSystemOne(stackDecision, Column.From(1)));

        var winDecision = Decision.For(Games.Play(1, 7, 2, 7, 3, 6), Player.Red);
        var win = DecisionPrompt.CriterionForSystemOne(winDecision, Column.From(4));
        Assert.Equal("Empty. A disc drops to row 0. Playing here wins immediately.", win);
        Assert.DoesNotContain("[[COL:", win, StringComparison.Ordinal);
        Assert.DoesNotContain("Column ", win, StringComparison.Ordinal);

        var blockDecision = Decision.For(Games.Play(7, 1, 7, 1, 6, 1), Player.Red);
        var block = DecisionPrompt.CriterionForSystemOne(blockDecision, Column.From(1));
        Assert.Equal(
            "3 disc(s) from the bottom: Y-Y-Y. Next disc lands on row 3. Playing here blocks an immediate opponent win.",
            block);
        Assert.DoesNotContain("[[COL:", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Column ", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Numbered_Criterion_still_carries_column_markers_for_chat()
    {
        var decision = Decision.For(Board.Empty, Player.Red);
        var numbered = DecisionPrompt.Criterion(decision, Column.From(4));
        Assert.Contains("[[COL:4]]", numbered);
        Assert.Contains("Column 4", numbered);
        Assert.DoesNotContain(
            "[[COL:",
            DecisionPrompt.CriterionForSystemOne(decision, Column.From(4)),
            StringComparison.Ordinal);
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
