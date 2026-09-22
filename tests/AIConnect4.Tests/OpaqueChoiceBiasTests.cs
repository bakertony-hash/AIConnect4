using System.Text.Json;

namespace AIConnect4.Tests;

public class OpaqueChoiceBiasTests
{
    private static readonly ChatTuning Tuning = ChatTuning.Omit;

    [Fact]
    public async Task Always_last_opaque_key_does_not_stack_a_single_column()
    {
        var handler = FakeHandler.FromRequest(LastOpaqueKeyReply);
        var client = FakeHandler.Client(handler);
        var runner = new GameRunner(
            new JevMoveSource(client, ModelCatalog.Jev, new Random(21)),
            new ChatMoveSource(client, ModelCatalog.Luna, Tuning));

        var ended = await runner.PlayAsync(CancellationToken.None);

        var columns = runner.Moves.Select(move => move.Column.Value).ToArray();
        Assert.True(columns.Length >= 7, $"moves: [{string.Join(',', columns)}]");
        Assert.NotEqual([7, 7, 7, 7, 7, 7, 6], columns.Take(7).ToArray());
        Assert.True(columns.Take(6).Distinct().Count() > 1, $"first six stacked: [{string.Join(',', columns.Take(6))}]");

        var firstBoard = BoardText(handler.Requests[0].Body);
        var laterBoard = BoardText(handler.Requests[3].Body);
        Assert.Contains(".......", firstBoard);
        Assert.NotEqual(firstBoard, laterBoard);
        Assert.Contains('R', laterBoard);
        Assert.Contains('Y', laterBoard);

        Assert.IsType<GameStatus.Finished>(ended);
    }

    [Fact]
    public async Task Last_opaque_key_resolves_through_the_request_map()
    {
        var handler = FakeHandler.FromRequest(LastOpaqueKeyReply);
        var source = new JevMoveSource(FakeHandler.Client(handler), ModelCatalog.Jev, new Random(22));

        var opening = await source.GetMoveAsync(Decision.For(Board.Empty, Player.Red), CancellationToken.None);
        var openingCriteria = handler.BodyOf(0).GetProperty("questions").GetProperty("column").GetProperty("criteria");
        var openingKeys = openingCriteria.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(["opt_a", "opt_b", "opt_c", "opt_d", "opt_e", "opt_f", "opt_g"], openingKeys);
        var openingLastKey = openingKeys[^1];
        var openingColumn = Column.From(ColumnFromCriterion(openingCriteria.GetProperty(openingLastKey).GetString()!));
        Assert.Equal(openingColumn, Assert.IsType<MoveReply.Chosen>(opening).Column);

        var afterSevenFull = Decision.For(Games.Play(7, 7, 7, 7, 7, 7), Player.Red);
        Assert.Equal([1, 2, 3, 4, 5, 6], afterSevenFull.Criteria.Select(column => column.Value).ToArray());

        var next = await source.GetMoveAsync(afterSevenFull, CancellationToken.None);
        var nextCriteria = handler.BodyOf(1).GetProperty("questions").GetProperty("column").GetProperty("criteria");
        var nextKeys = nextCriteria.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(["opt_a", "opt_b", "opt_c", "opt_d", "opt_e", "opt_f"], nextKeys);
        var nextLastKey = nextKeys[^1];
        var nextColumn = Column.From(ColumnFromCriterion(nextCriteria.GetProperty(nextLastKey).GetString()!));
        Assert.Equal(nextColumn, Assert.IsType<MoveReply.Chosen>(next).Column);
    }

    [Fact]
    public void Source_tree_has_no_silent_legal_column_fallback()
    {
        var hits =
            from path in Directory.GetFiles(RepoSrcDirectory(), "*.cs", SearchOption.AllDirectories)
            from row in File.ReadAllLines(path).Select((line, index) => (line, number: index + 1))
            where row.line.Contains("Criteria.First", StringComparison.Ordinal)
                || row.line.Contains("Criteria.Last", StringComparison.Ordinal)
                || row.line.Contains("LegalColumns().First", StringComparison.Ordinal)
                || row.line.Contains("LegalColumns().Last", StringComparison.Ordinal)
            select $"{path}:{row.number}:{row.line.Trim()}";

        Assert.Empty(hits);
    }

    private static string RepoSrcDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var src = Path.Combine(dir.FullName, "src");
            if (Directory.Exists(src))
            {
                return src;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find repo src/ walking up from {AppContext.BaseDirectory}");
    }

    private static string LastOpaqueKeyReply(string requestBody)
    {
        using var document = JsonDocument.Parse(requestBody);
        var root = document.RootElement;
        if (root.TryGetProperty("questions", out var questions))
        {
            var choice = questions.GetProperty("column").GetProperty("criteria").EnumerateObject().Last().Name;
            return FakeHandler.JevChoice(choice);
        }

        var column = root.GetProperty("response_format").GetProperty("json_schema")
            .GetProperty("schema").GetProperty("properties").GetProperty("column").GetProperty("enum")
            .EnumerateArray().Select(value => value.GetInt32()).Last();
        return FakeHandler.ChatReply($$"""{"column":{{column}},"reason":"last legal"}""");
    }

    private static int ColumnFromCriterion(string text)
    {
        const string marker = "[[COL:";
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        var numberStart = start + marker.Length;
        var end = text.IndexOf(']', numberStart);
        return int.Parse(text[numberStart..end]);
    }

    private static string BoardText(string requestBody)
    {
        using var document = JsonDocument.Parse(requestBody);
        var root = document.RootElement;
        if (root.TryGetProperty("state", out var state) && state.TryGetProperty("board", out var board))
        {
            return board.GetString() ?? "";
        }

        foreach (var message in root.GetProperty("messages").EnumerateArray())
        {
            if (message.GetProperty("role").GetString() == "user")
            {
                return message.GetProperty("content").GetString() ?? "";
            }
        }

        return requestBody;
    }
}
