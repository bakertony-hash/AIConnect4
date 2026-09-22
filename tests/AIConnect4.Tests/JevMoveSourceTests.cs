using System.Text.Json;

namespace AIConnect4.Tests;

public class JevMoveSourceTests
{
    private const string ChoiceReply =
        """{"model":"typesafe/jev-1.13","answers":{"column":{"type":"choice","choice":"4","confidence":0.82,"probabilities":{"2":0.05,"3":0.13,"4":0.82}}},"usage":{"input_tokens":1,"output_tokens":1}}""";

    private static readonly Decision Opening = Decision.For(Board.Empty, Player.Red);

    private static readonly Decision ColumnOneFull = Decision.For(Games.Play(1, 1, 1, 1, 1, 1), Player.Red);

    private static string Choosing(string choice) =>
        JsonSerializer.Serialize(new { answers = new { column = new { type = "choice", choice } } });

    private static async Task<(MoveReply Reply, JsonElement Body)> Ask(Decision decision, string reply)
    {
        var handler = FakeHandler.Json(reply);
        var source = new JevMoveSource(FakeHandler.Client(handler), ModelCatalog.Jev);
        var result = await source.GetMoveAsync(decision, CancellationToken.None);
        return (result, handler.BodyOf(0));
    }

    [Fact]
    public async Task Request_is_one_choice_question_over_the_legal_columns()
    {
        var (_, body) = await Ask(ColumnOneFull, ChoiceReply);

        Assert.Equal("typesafe/jev-1.13", body.GetProperty("model").GetString());
        var column = body.GetProperty("questions").GetProperty("column");
        Assert.Equal("choice", column.GetProperty("type").GetString());
        Assert.Equal("Which legal column should you play?", column.GetProperty("instructions").GetString());
        Assert.Equal(["2", "3", "4", "5", "6", "7"], column.GetProperty("criteria").EnumerateObject().Select(property => property.Name));
        Assert.Equal("Drop your disc in column 2", column.GetProperty("criteria").GetProperty("2").GetString());
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
    public async Task Empty_board_offers_all_seven_columns_and_no_last_move()
    {
        var (_, body) = await Ask(Opening, ChoiceReply);

        Assert.Equal(["1", "2", "3", "4", "5", "6", "7"], body.GetProperty("questions").GetProperty("column").GetProperty("criteria").EnumerateObject().Select(property => property.Name));
        Assert.False(body.GetProperty("state").TryGetProperty("last_move", out _));
    }

    [Fact]
    public async Task Retry_carries_the_prior_failure_as_previous_attempt()
    {
        var (_, body) = await Ask(ColumnOneFull.Retry(new MoveFailure.IllegalColumn(Column.From(1))), ChoiceReply);

        Assert.Equal("Column 1 is full. Pick a legal column.", body.GetProperty("state").GetProperty("previous_attempt").GetString());
    }

    [Fact]
    public async Task Choice_becomes_the_column_with_confidence_and_probabilities_as_reason()
    {
        var (reply, _) = await Ask(ColumnOneFull, ChoiceReply);

        Assert.Equal(new MoveReply.Chosen(Column.From(4), "confidence 0.82; 2: 0.05, 3: 0.13, 4: 0.82"), reply);
    }

    [Fact]
    public async Task Full_column_is_illegal()
    {
        var (reply, _) = await Ask(ColumnOneFull, Choosing("1"));

        Assert.Equal(new MoveReply.Failed(new MoveFailure.IllegalColumn(Column.From(1))), reply);
    }

    [Fact]
    public async Task Out_of_range_choice_is_unparseable()
    {
        var (reply, _) = await Ask(Opening, Choosing("9"));

        Assert.Equal(new MoveReply.Failed(new MoveFailure.Unparseable("9")), reply);
    }

    [Fact]
    public async Task Blank_choice_is_empty()
    {
        var (reply, _) = await Ask(Opening, Choosing(""));

        Assert.Equal(new MoveReply.Failed(new MoveFailure.Empty()), reply);
    }

    [Fact]
    public async Task Missing_column_answer_is_unparseable_quoting_the_body()
    {
        var (reply, _) = await Ask(Opening, """{"answers":{}}""");

        var failed = Assert.IsType<MoveReply.Failed>(reply);
        var unparseable = Assert.IsType<MoveFailure.Unparseable>(failed.Failure);
        Assert.Contains("answers", unparseable.Answer);
    }

    [Fact]
    public async Task Non_json_body_is_unparseable()
    {
        var (reply, _) = await Ask(Opening, "<html>");

        Assert.Equal(new MoveReply.Failed(new MoveFailure.Unparseable("<html>")), reply);
    }
}
