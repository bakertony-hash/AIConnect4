using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AIConnect4.Tests;

internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly Func<int, string, CancellationToken, Task<HttpResponseMessage>> _reply;

    private FakeHandler(Func<int, string, CancellationToken, Task<HttpResponseMessage>> reply) => _reply = reply;

    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    public static FakeHandler Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _, _) => Task.FromResult(Response(body, status)));

    public static FakeHandler Sequence(params string[] bodies) =>
        new((index, _, _) => index < bodies.Length
            ? Task.FromResult(Response(bodies[index], HttpStatusCode.OK))
            : throw new InvalidOperationException($"Request {index + 1} has no scripted reply."));

    public static FakeHandler Hanging() =>
        new(async (_, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new UnreachableException();
        });

    /// <summary>Builds each reply from the outbound JSON body (for stacking / last-legal repros).</summary>
    public static FakeHandler FromRequest(Func<string, string> map) =>
        new((_, body, _) => Task.FromResult(Response(map(body), HttpStatusCode.OK)));

    public static OpenRouterClient Client(FakeHandler handler, TimeSpan? timeout = null) =>
        new(OpenRouterHttp.Create("test-key", handler), timeout ?? OpenRouterClient.DefaultTimeout);

    public static string ChatReply(string? content) =>
        JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content } } } });

    public static string JevChoice(string choice) =>
        JsonSerializer.Serialize(new { answers = new { column = new { type = "choice", choice, confidence = 0.5 } } });

    public JsonElement BodyOf(int index) => JsonDocument.Parse(Requests[index].Body).RootElement;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var index = Requests.Count;
        Requests.Add((request, body));
        return await _reply(index, body, cancellationToken);
    }

    private static HttpResponseMessage Response(string body, HttpStatusCode status) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
