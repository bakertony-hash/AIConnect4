using System.Text.Json;

namespace AIConnect4.Tests;

public class JevMoveSourceTests
{
    private static readonly Decision Opening = Decision.For(Board.Empty, Player.Red);

    private static readonly Decision ColumnOneFull = Decision.For(Games.Play(1, 1, 1, 1, 1, 1), Player.Red);

    private static string Choosing(string choice) =>
        JsonSerializer.Serialize(new { answers = new { column = new { type = "choice", choice } } });

    private static string ChoiceWith(string choice, double confidence, Dictionary<string, double> probabilities) =>
        JsonSerializer.Serialize(new
        {
            model = "typesafe/jev-1.13",
            answers = new
            {
                column = new
                {
                    type = "choice",
                    choice,
                    confidence,
                    probabilities,
                },
            },
            usage = new { input_tokens = 1, output_tokens = 1 },
        });

    private static async Task<(MoveReply Reply, JsonElement Body)> Ask(
        Decision decision,
        string reply,
        Random? random = null)
    {
        var handler = FakeHandler.Json(reply);
        var source = new JevMoveSource(FakeHandler.Client(handler), ModelCatalog.Jev, random);
        var result = await source.GetMoveAsync(decision, CancellationToken.None);
        return (result, handler.BodyOf(0));
    }

    private static int[] ColumnOrder(Decision decision, Random random) =>
        JevMoveSource.OpaqueCriteria.Assign(decision, random).KeyToColumn.Values.Select(column => column.Value).ToArray();

    [Fact]
    public async Task Request_is_one_choice_question_with_opaque_keys()
    {
        var seed = new Random(1);
        var expected = JevMoveSource.OpaqueCriteria.Assign(ColumnOneFull, new Random(1));
        var (_, body) = await Ask(ColumnOneFull, Choosing("opt_a"), seed);

        Assert.Equal("typesafe/jev-1.13", body.GetProperty("model").GetString());
        var column = body.GetProperty("questions").GetProperty("column");
        Assert.Equal("choice", column.GetProperty("type").GetString());
        var keys = column.GetProperty("criteria").EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(["opt_a", "opt_b", "opt_c", "opt_d", "opt_e", "opt_f"], keys);
        Assert.All(keys, key => Assert.DoesNotContain(key, new[] { "1", "2", "3", "4", "5", "6", "7" }));
        Assert.StartsWith("Which legal column should you play?", column.GetProperty("instructions").GetString());
        Assert.Contains("state.board", column.GetProperty("instructions").GetString());
        Assert.Equal(new HashSet<int> { 2, 3, 4, 5, 6, 7 }, expected.KeyToColumn.Values.Select(c => c.Value).ToHashSet());
        Assert.Equal("Empty. A disc drops to row 0.", column.GetProperty("criteria").GetProperty("opt_a").GetString());
        Assert.All(
            column.GetProperty("criteria").EnumerateObject().Select(property => property.Value.GetString() ?? ""),
            text =>
            {
                Assert.DoesNotContain("[[COL:", text, StringComparison.Ordinal);
                Assert.DoesNotContain("Column ", text, StringComparison.Ordinal);
            });
        var state = body.GetProperty("state");
        Assert.Equal(Decision.Rules, state.GetProperty("rules").GetString());
        Assert.Equal("You are Red (R). It is your move.", state.GetProperty("you_are").GetString());
        Assert.Equal("Yellow played column 1.", state.GetProperty("last_move").GetString());
        Assert.StartsWith("Board, top row first. R = Red, Y = Yellow, . = empty.\nY......\nR......", state.GetProperty("board").GetString());
        Assert.False(body.TryGetProperty("reasoning", out _));
        Assert.False(body.TryGetProperty("provider", out _));
        Assert.False(state.TryGetProperty("previous_attempt", out _));
    }

    [Fact]
    public async Task Same_decision_twice_with_different_seeds_yields_different_key_orders()
    {
        var (_, bodyA) = await Ask(Opening, Choosing("opt_a"), new Random(1));
        var (_, bodyB) = await Ask(Opening, Choosing("opt_a"), new Random(2));

        var orderA = ColumnOrder(Opening, new Random(1));
        var orderB = ColumnOrder(Opening, new Random(2));
        var keysA = bodyA.GetProperty("questions").GetProperty("column").GetProperty("criteria")
            .EnumerateObject().Select(property => property.Name).ToArray();
        var keysB = bodyB.GetProperty("questions").GetProperty("column").GetProperty("criteria")
            .EnumerateObject().Select(property => property.Name).ToArray();

        Assert.Equal(7, orderA.Length);
        Assert.Equal(new HashSet<int> { 1, 2, 3, 4, 5, 6, 7 }, orderA.ToHashSet());
        Assert.Equal(new HashSet<int> { 1, 2, 3, 4, 5, 6, 7 }, orderB.ToHashSet());
        Assert.NotEqual(orderA, orderB);
        Assert.Equal(["opt_a", "opt_b", "opt_c", "opt_d", "opt_e", "opt_f", "opt_g"], keysA);
        Assert.Equal(keysA, keysB);
    }

    [Fact]
    public async Task Criteria_use_SystemOne_stack_facts_and_win_block_without_digit_column_markers()
    {
        var stackDecision = Decision.For(Games.Play(4, 4, 4), Player.Yellow);
        var stackAssignment = JevMoveSource.OpaqueCriteria.Assign(stackDecision, new Random(3));
        var (_, stackBody) = await Ask(stackDecision, Choosing("opt_a"), new Random(3));
        var stackCriteria = stackBody.GetProperty("questions").GetProperty("column").GetProperty("criteria");
        var keyForFour = stackAssignment.KeyToColumn.Single(pair => pair.Value.Value == 4).Key;
        var keyForOne = stackAssignment.KeyToColumn.Single(pair => pair.Value.Value == 1).Key;
        Assert.Equal(
            "3 disc(s) from the bottom: R-Y-R. Next disc lands on row 3.",
            stackCriteria.GetProperty(keyForFour).GetString());
        Assert.Equal(
            "Empty. A disc drops to row 0.",
            stackCriteria.GetProperty(keyForOne).GetString());

        var winDecision = Decision.For(Games.Play(1, 7, 2, 7, 3, 6), Player.Red);
        var (_, winBody) = await Ask(winDecision, Choosing("opt_a"), new Random(4));
        var winTexts = winBody.GetProperty("questions").GetProperty("column").GetProperty("criteria")
            .EnumerateObject().Select(property => property.Value.GetString() ?? "").ToList();
        Assert.Contains(winTexts, text => text == "Empty. A disc drops to row 0. Playing here wins immediately.");
        Assert.All(winTexts, text =>
        {
            Assert.DoesNotContain("[[COL:", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Column ", text, StringComparison.Ordinal);
        });

        var blockDecision = Decision.For(Games.Play(7, 1, 7, 1, 6, 1), Player.Red);
        var (_, blockBody) = await Ask(blockDecision, Choosing("opt_a"), new Random(5));
        var blockTexts = blockBody.GetProperty("questions").GetProperty("column").GetProperty("criteria")
            .EnumerateObject().Select(property => property.Value.GetString() ?? "").ToList();
        Assert.Contains(
            blockTexts,
            text => text == "3 disc(s) from the bottom: Y-Y-Y. Next disc lands on row 3. Playing here blocks an immediate opponent win.");
        Assert.All(blockTexts, text =>
        {
            Assert.DoesNotContain("[[COL:", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Column ", text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Empty_board_offers_seven_opaque_keys_and_no_last_move()
    {
        var (_, body) = await Ask(Opening, Choosing("opt_a"), new Random(6));

        var keys = body.GetProperty("questions").GetProperty("column").GetProperty("criteria")
            .EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(["opt_a", "opt_b", "opt_c", "opt_d", "opt_e", "opt_f", "opt_g"], keys);
        Assert.False(body.GetProperty("state").TryGetProperty("last_move", out _));
        var values = body.GetProperty("questions").GetProperty("column").GetProperty("criteria")
            .EnumerateObject().Select(property => property.Value.GetString() ?? "").Distinct().ToArray();
        Assert.Equal(["Empty. A disc drops to row 0."], values);
    }

    [Fact]
    public void After_last_move_omits_that_column_from_opaque_criteria()
    {
        var decision = Decision.For(Games.Play(7), Player.Yellow);
        var assignment = JevMoveSource.OpaqueCriteria.Assign(decision, new Random(21));

        Assert.Equal(6, assignment.KeyToColumn.Count);
        Assert.DoesNotContain(7, assignment.KeyToColumn.Values.Select(column => column.Value));
        Assert.Equal(new HashSet<int> { 1, 2, 3, 4, 5, 6 }, assignment.KeyToColumn.Values.Select(c => c.Value).ToHashSet());
    }

    [Fact]
    public void Opening_still_offers_all_seven_columns()
    {
        var assignment = JevMoveSource.OpaqueCriteria.Assign(Opening, new Random(22));

        Assert.Equal(7, assignment.KeyToColumn.Count);
        Assert.Equal(new HashSet<int> { 1, 2, 3, 4, 5, 6, 7 }, assignment.KeyToColumn.Values.Select(c => c.Value).ToHashSet());
    }

    [Fact]
    public void Last_move_column_stays_when_it_is_the_only_block()
    {
        var decision = Decision.For(Games.Play(1, 7, 3, 7, 5, 7), Player.Red);
        Assert.Equal(Column.From(7), decision.LastMove!.Value.Position.Column);
        Assert.Contains("blocks an immediate opponent win", DecisionPrompt.CriterionForSystemOne(decision, Column.From(7)));
        Assert.All(
            decision.Criteria.Where(column => column.Value != 7),
            column =>
            {
                var text = DecisionPrompt.CriterionForSystemOne(decision, column);
                Assert.DoesNotContain("wins immediately", text);
                Assert.DoesNotContain("blocks an immediate opponent win", text);
            });

        var assignment = JevMoveSource.OpaqueCriteria.Assign(decision, new Random(23));

        Assert.Contains(7, assignment.KeyToColumn.Values.Select(column => column.Value));
    }

    [Fact]
    public async Task Retry_carries_the_prior_failure_as_previous_attempt()
    {
        var (_, body) = await Ask(
            ColumnOneFull.Retry(new MoveFailure.IllegalColumn(Column.From(1))),
            Choosing("opt_a"),
            new Random(7));

        Assert.Equal("Column 1 is full. Pick a legal column.", body.GetProperty("state").GetProperty("previous_attempt").GetString());
    }

    [Fact]
    public async Task Opaque_choice_maps_to_the_assigned_column_with_reason()
    {
        var assignment = JevMoveSource.OpaqueCriteria.Assign(ColumnOneFull, new Random(8));
        var keyForFour = assignment.KeyToColumn.Single(pair => pair.Value.Value == 4).Key;
        var handler = FakeHandler.FromRequest(_ => ChoiceWith(keyForFour, 0.82, new Dictionary<string, double>
        {
            [keyForFour] = 0.82,
            ["opt_a"] = 0.05,
            ["opt_b"] = 0.13,
        }));
        var source = new JevMoveSource(FakeHandler.Client(handler), ModelCatalog.Jev, new Random(8));
        var reply = await source.GetMoveAsync(ColumnOneFull, CancellationToken.None);

        var chosen = Assert.IsType<MoveReply.Chosen>(reply);
        Assert.Equal(Column.From(4), chosen.Column);
        Assert.Contains("confidence 0.82", chosen.Reason);
        Assert.Contains("opt_", chosen.Reason);
    }

    [Fact]
    public async Task Numeric_choice_is_unparseable()
    {
        var (reply, _) = await Ask(Opening, Choosing("4"), new Random(10));

        Assert.Equal(new MoveReply.Failed(new MoveFailure.Unparseable("4")), reply);
    }

    [Fact]
    public async Task Unknown_opaque_key_is_unparseable()
    {
        var (reply, _) = await Ask(Opening, Choosing("opt_z"), new Random(11));

        Assert.Equal(new MoveReply.Failed(new MoveFailure.Unparseable("opt_z")), reply);
    }

    [Fact]
    public async Task Blank_choice_is_empty()
    {
        var (reply, _) = await Ask(Opening, Choosing(""), new Random(12));

        Assert.Equal(new MoveReply.Failed(new MoveFailure.Empty()), reply);
    }

    [Fact]
    public async Task Missing_column_answer_is_unparseable_quoting_the_body()
    {
        var (reply, _) = await Ask(Opening, """{"answers":{}}""", new Random(13));

        var failed = Assert.IsType<MoveReply.Failed>(reply);
        var unparseable = Assert.IsType<MoveFailure.Unparseable>(failed.Failure);
        Assert.Contains("answers", unparseable.Answer);
    }

    [Fact]
    public async Task Non_json_body_is_unparseable()
    {
        var (reply, _) = await Ask(Opening, "<html>", new Random(14));

        Assert.Equal(new MoveReply.Failed(new MoveFailure.Unparseable("<html>")), reply);
    }
}
