using System.Text.Json;

namespace AIConnect4.Tests;

/// <summary>
/// Reproduces the reported stacking pattern when both sides always answer with the last
/// legal column from the request (last System One criteria key / last chat schema enum).
/// </summary>
public class StackingReproTests
{
    private static readonly ChatTuning Tuning = ChatTuning.Omit;

    [Fact]
    public async Task Always_last_legal_stacks_column_7_then_moves_left()
    {
        var handler = FakeHandler.FromRequest(LastLegalReply);
        var client = FakeHandler.Client(handler);
        var runner = new GameRunner(
            new JevMoveSource(client, ModelCatalog.Jev),
            new ChatMoveSource(client, ModelCatalog.Luna, Tuning));

        var ended = await runner.PlayAsync(CancellationToken.None);

        var columns = runner.Moves.Select(move => move.Column.Value).ToArray();
        Assert.True(columns.Length >= 7, $"moves: [{string.Join(',', columns)}]");
        Assert.Equal([7, 7, 7, 7, 7, 7, 6], columns.Take(7).ToArray());

        var firstBoard = BoardText(handler.Requests[0].Body);
        var laterBoard = BoardText(handler.Requests[3].Body);
        Assert.Contains(".......", firstBoard);
        Assert.NotEqual(firstBoard, laterBoard);
        Assert.Contains('R', laterBoard);
        Assert.Contains('Y', laterBoard);

        Assert.IsType<GameStatus.Finished>(ended);
    }

    [Fact]
    public async Task Last_legal_reply_tracks_shrinking_criteria_not_a_host_fallback()
    {
        var handler = FakeHandler.FromRequest(LastLegalReply);
        var source = new JevMoveSource(FakeHandler.Client(handler), ModelCatalog.Jev);

        var opening = await source.GetMoveAsync(Decision.For(Board.Empty, Player.Red), CancellationToken.None);
        Assert.Equal(Column.From(7), Assert.IsType<MoveReply.Chosen>(opening).Column);
        Assert.Equal(
            ["1", "2", "3", "4", "5", "6", "7"],
            handler.BodyOf(0).GetProperty("questions").GetProperty("column").GetProperty("criteria")
                .EnumerateObject().Select(property => property.Name).ToArray());

        var afterSevenFull = Decision.For(Games.Play(7, 7, 7, 7, 7, 7), Player.Red);
        Assert.Equal([1, 2, 3, 4, 5, 6], afterSevenFull.Criteria.Select(column => column.Value).ToArray());

        var next = await source.GetMoveAsync(afterSevenFull, CancellationToken.None);
        Assert.Equal(Column.From(6), Assert.IsType<MoveReply.Chosen>(next).Column);
        Assert.Equal(
            ["1", "2", "3", "4", "5", "6"],
            handler.BodyOf(1).GetProperty("questions").GetProperty("column").GetProperty("criteria")
                .EnumerateObject().Select(property => property.Name).ToArray());
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

    private static string LastLegalReply(string requestBody)
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
