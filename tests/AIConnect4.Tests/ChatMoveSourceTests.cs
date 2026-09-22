using System.Text.Json;

namespace AIConnect4.Tests;

public class ChatMoveSourceTests
{
    private static readonly ChatTuning MediumThroughput = new(Effort.Medium, Speed.Throughput);

    private static readonly Decision Opening = Decision.For(Board.Empty, Player.Red);

    private static readonly Decision ColumnOneFull = Decision.For(Games.Play(1, 1, 1, 1, 1, 1), Player.Red);

    private static async Task<(MoveReply Reply, JsonElement Body)> Ask(
        Decision decision,
        string? content,
        ModelProfile.Chat? profile = null,
        ChatTuning? tuning = null,
        string? rawReply = null)
    {
        var handler = FakeHandler.Json(rawReply ?? FakeHandler.ChatReply(content));
        var source = new ChatMoveSource(FakeHandler.Client(handler), profile ?? ModelCatalog.Luna, tuning ?? MediumThroughput);
        var reply = await source.GetMoveAsync(decision, CancellationToken.None);
        return (reply, handler.BodyOf(0));
    }

    private static string UserMessage(JsonElement body) => body.GetProperty("messages")[1].GetProperty("content").GetString()!;

    [Fact]
    public async Task Request_carries_the_decision_schema_effort_and_speed()
    {
        var handler = FakeHandler.Json(FakeHandler.ChatReply("""{"column": 4}"""));
        var source = new ChatMoveSource(FakeHandler.Client(handler), ModelCatalog.Luna, MediumThroughput);

        await source.GetMoveAsync(ColumnOneFull, CancellationToken.None);

        Assert.Equal(new Uri("https://openrouter.ai/api/v1/chat/completions"), handler.Requests[0].Request.RequestUri);
        var body = handler.BodyOf(0);
        Assert.Equal("openai/gpt-5.6-luna", body.GetProperty("model").GetString());
        var messages = body.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Contains("You are Red (R). It is your move.", messages[0].GetProperty("content").GetString());
        Assert.Contains(Decision.Rules, messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Contains("Legal columns: 2, 3, 4, 5, 6, 7.", UserMessage(body));
        Assert.Contains("Which legal column should you play?", UserMessage(body));
        Assert.Contains("Study the board grid above.", UserMessage(body));
        Assert.Contains("Yellow played column 1.", UserMessage(body));
        Assert.EndsWith("""Answer with JSON only: {"column": <one of the legal columns>, "reason": "<one sentence>"}.""", UserMessage(body));
        var format = body.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        var schema = format.GetProperty("json_schema");
        Assert.Equal("column_choice", schema.GetProperty("name").GetString());
        Assert.True(schema.GetProperty("strict").GetBoolean());
        var column = schema.GetProperty("schema").GetProperty("properties").GetProperty("column");
        Assert.Equal("integer", column.GetProperty("type").GetString());
        Assert.Equal([2, 3, 4, 5, 6, 7], column.GetProperty("enum").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal(["column", "reason"], schema.GetProperty("schema").GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.False(schema.GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("medium", body.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal("throughput", body.GetProperty("provider").GetProperty("sort").GetString());
    }

    [Fact]
    public async Task Omit_tuning_sends_neither_reasoning_nor_provider()
    {
        var (_, body) = await Ask(Opening, """{"column": 4}""", tuning: ChatTuning.Omit);

        Assert.False(body.TryGetProperty("reasoning", out _));
        Assert.False(body.TryGetProperty("provider", out _));
    }

    [Fact]
    public async Task Profile_without_effort_or_speed_ignores_the_tuning()
    {
        var profile = new ModelProfile.Chat("x", "x/y", SupportsEffort: false, SupportsSpeed: false, AnswerFormat.JsonSchema);

        var (_, body) = await Ask(Opening, """{"column": 4}""", profile, new ChatTuning(Effort.High, Speed.LowLatency));

        Assert.False(body.TryGetProperty("reasoning", out _));
        Assert.False(body.TryGetProperty("provider", out _));
    }

    [Fact]
    public async Task Low_latency_sorts_providers_by_latency()
    {
        var (_, body) = await Ask(Opening, """{"column": 4}""", tuning: new ChatTuning(Effort.XHigh, Speed.LowLatency));

        Assert.Equal("latency", body.GetProperty("provider").GetProperty("sort").GetString());
        Assert.Equal("xhigh", body.GetProperty("reasoning").GetProperty("effort").GetString());
    }

    [Fact]
    public async Task Json_object_format_has_no_schema()
    {
        var profile = ModelCatalog.Luna with { AnswerFormat = AnswerFormat.JsonObject };

        var (_, body) = await Ask(Opening, """{"column": 4}""", profile);

        var format = body.GetProperty("response_format");
        Assert.Equal("json_object", format.GetProperty("type").GetString());
        Assert.False(format.TryGetProperty("json_schema", out _));
    }

    [Fact]
    public async Task Text_format_has_no_response_format_and_asks_for_the_number()
    {
        var profile = ModelCatalog.Luna with { AnswerFormat = AnswerFormat.Text };

        var (_, body) = await Ask(Opening, "4", profile);

        Assert.False(body.TryGetProperty("response_format", out _));
        Assert.EndsWith("Answer with the column number only.", UserMessage(body));
    }

    [Fact]
    public async Task Retry_puts_the_prior_failure_in_the_user_message()
    {
        var (_, body) = await Ask(ColumnOneFull.Retry(new MoveFailure.IllegalColumn(Column.From(1))), """{"column": 4}""");

        Assert.Contains("Column 1 is full. Pick a legal column.", UserMessage(body));
    }

    [Fact]
    public async Task Json_column_and_reason_are_chosen()
    {
        var (reply, _) = await Ask(Opening, """{"column": 5, "reason": "centre control"}""");

        Assert.Equal(new MoveReply.Chosen(Column.From(5), "centre control"), reply);
    }

    [Fact]
    public async Task Json_column_as_string_is_chosen_without_reason()
    {
        var (reply, _) = await Ask(Opening, """{"column": "3"}""");

        Assert.Equal(new MoveReply.Chosen(Column.From(3)), reply);
    }

    [Fact]
    public async Task Fenced_json_is_unwrapped()
    {
        var (reply, _) = await Ask(Opening, "```json\n{\"column\": 6, \"reason\": \"r\"}\n```");

        Assert.Equal(new MoveReply.Chosen(Column.From(6), "r"), reply);
    }

    [Fact]
    public async Task Bare_number_on_a_text_profile_is_chosen_without_reason()
    {
        var profile = Assert.IsType<ModelProfile.Chat>(ModelCatalog.Custom("mistralai/mistral-large"));

        var (reply, _) = await Ask(Opening, "4", profile);

        Assert.Equal(new MoveReply.Chosen(Column.From(4)), reply);
    }

    [Fact]
    public async Task Prose_with_a_number_is_chosen_with_the_prose_as_reason()
    {
        var (reply, _) = await Ask(Opening, "Column 3 looks best.");

        Assert.Equal(new MoveReply.Chosen(Column.From(3), "Column 3 looks best."), reply);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Blank_or_null_content_is_empty(string? content)
    {
        var (reply, _) = await Ask(Opening, content);

        Assert.Equal(new MoveReply.Failed(new MoveFailure.Empty()), reply);
    }

    [Fact]
    public async Task No_choices_is_empty()
    {
        var (reply, _) = await Ask(Opening, content: null, rawReply: """{"choices":[]}""");

        Assert.Equal(new MoveReply.Failed(new MoveFailure.Empty()), reply);
    }

    [Theory]
    [InlineData("I pass.")]
    [InlineData("Column 99999999999 for me.")]
    public async Task Prose_without_a_board_column_is_unparseable(string content)
    {
        var (reply, _) = await Ask(Opening, content);

        Assert.Equal(new MoveReply.Failed(new MoveFailure.Unparseable(content)), reply);
    }

    [Fact]
    public async Task Full_column_is_illegal()
    {
        var (reply, _) = await Ask(ColumnOneFull, """{"column": 1}""");

        Assert.Equal(new MoveReply.Failed(new MoveFailure.IllegalColumn(Column.From(1))), reply);
    }

    [Theory]
    [InlineData("""{"column": 9}""")]
    [InlineData("""{"column": 3.5}""")]
    public async Task Json_column_that_is_not_a_board_column_is_unparseable(string content)
    {
        var (reply, _) = await Ask(Opening, content);

        Assert.Equal(new MoveReply.Failed(new MoveFailure.Unparseable(content)), reply);
    }

    [Fact]
    public async Task Two_chat_sides_play_a_whole_game_through_the_runner()
    {
        var handler = FakeHandler.Sequence([.. Games.RedVerticalWin.Select(column => FakeHandler.ChatReply($$"""{"column":{{column}}}"""))]);
        var client = FakeHandler.Client(handler);
        var runner = new GameRunner(
            new ChatMoveSource(client, ModelCatalog.Luna, MediumThroughput),
            new ChatMoveSource(client, ModelCatalog.Luna, MediumThroughput));

        var ended = await runner.PlayAsync(CancellationToken.None);

        var finished = Assert.IsType<GameStatus.Finished>(ended);
        var win = Assert.IsType<GameResult.Win>(finished.Result);
        Assert.Equal(Player.Red, win.Winner);
        Assert.Equal(7, handler.Requests.Count);
    }
}
