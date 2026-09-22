using System.Net;

namespace AIConnect4.Tests;

public class OpenRouterClientTests
{
    private static readonly Decision Opening = Decision.For(Board.Empty, Player.Red);

    [Fact]
    public async Task Non_2xx_status_is_faulted_with_status_and_body()
    {
        var handler = FakeHandler.Json("""{"error":"boom"}""", HttpStatusCode.InternalServerError);
        var source = new JevMoveSource(FakeHandler.Client(handler), ModelCatalog.Jev);

        var reply = await source.GetMoveAsync(Opening, CancellationToken.None);

        var failed = Assert.IsType<MoveReply.Failed>(reply);
        var faulted = Assert.IsType<MoveFailure.Faulted>(failed.Failure);
        Assert.Contains("500", faulted.Message);
        Assert.Contains("boom", faulted.Message);
    }

    [Fact]
    public async Task Exceeding_the_per_call_timeout_is_a_timeout_failure()
    {
        var source = new JevMoveSource(FakeHandler.Client(FakeHandler.Hanging(), TimeSpan.FromMilliseconds(50)), ModelCatalog.Jev);

        var reply = await source.GetMoveAsync(Opening, CancellationToken.None);

        Assert.Equal(new MoveReply.Failed(new MoveFailure.Timeout()), reply);
    }

    [Fact]
    public async Task External_cancellation_propagates()
    {
        var source = new JevMoveSource(FakeHandler.Client(FakeHandler.Hanging()), ModelCatalog.Jev);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.GetMoveAsync(Opening, cts.Token));
    }

    [Fact]
    public async Task Requests_go_to_openrouter_as_json_with_the_bearer_key()
    {
        var handler = FakeHandler.Json("""{"answers":{}}""");
        var source = new JevMoveSource(FakeHandler.Client(handler), ModelCatalog.Jev);

        await source.GetMoveAsync(Opening, CancellationToken.None);

        var (request, _) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://openrouter.ai/api/v1/systemone"), request.RequestUri);
        Assert.Equal("Bearer test-key", request.Headers.Authorization?.ToString());
        Assert.Equal("application/json", request.Content?.Headers.ContentType?.MediaType);
    }
}
