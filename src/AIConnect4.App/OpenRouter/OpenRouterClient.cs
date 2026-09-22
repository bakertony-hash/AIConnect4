using System.Text.Json;
using System.Text.Json.Serialization;
using AIConnect4.Core;

namespace AIConnect4.App.OpenRouter;

/// <summary>
/// One POST to OpenRouter with the transport failures already typed. Adapters send a request body and get back either
/// the response body or a <see cref="MoveFailure"/> they return unchanged. External cancellation propagates as
/// <see cref="OperationCanceledException"/> so the runner records Cancelled, not Timeout.
/// </summary>
public sealed class OpenRouterClient
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;

    public OpenRouterClient(HttpClient http)
        : this(http, DefaultTimeout)
    {
    }

    public OpenRouterClient(HttpClient http, TimeSpan timeout)
    {
        _http = http;
        _timeout = timeout;
    }

    /// <summary>
    /// Serialises <paramref name="body"/> with <see cref="Json"/> and posts it to <paramref name="path"/> relative to the
    /// client's base address. A 2xx returns <see cref="CallOutcome.Body"/>. Any other status returns
    /// <see cref="MoveFailure.Faulted"/> naming the status and the start of the response. Exceeding the per-call timeout
    /// returns <see cref="MoveFailure.Timeout"/>. A network fault returns <see cref="MoveFailure.Faulted"/>.
    /// </summary>
    public Task<CallOutcome> PostAsync(string path, object body, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}

public abstract record CallOutcome
{
    private CallOutcome()
    {
    }

    public sealed record Body(string Json) : CallOutcome;

    public sealed record Failed(MoveFailure Failure) : CallOutcome;
}
