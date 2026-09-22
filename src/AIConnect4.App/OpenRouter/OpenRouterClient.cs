using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AIConnect4.Core;

namespace AIConnect4.App.OpenRouter;

/// <summary>
/// One POST to OpenRouter with the transport failures already typed. External cancellation propagates as
/// <see cref="OperationCanceledException"/>.
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
    public async Task<CallOutcome> PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);
        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(body, body.GetType(), Json), Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(path, content, timeoutCts.Token);
            var text = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            return response.IsSuccessStatusCode
                ? new CallOutcome.Body(text)
                : new CallOutcome.Failed(new MoveFailure.Faulted($"HTTP {(int)response.StatusCode} {response.StatusCode}: {Snippet(text)}"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new CallOutcome.Failed(new MoveFailure.Timeout());
        }
        catch (HttpRequestException exception)
        {
            return new CallOutcome.Failed(new MoveFailure.Faulted(exception.Message));
        }
    }

    internal static string Snippet(string text)
    {
        var collapsed = Regex.Replace(text, @"\s+", " ").Trim();
        return collapsed.Length <= 200 ? collapsed : collapsed[..200];
    }
}

public abstract record CallOutcome
{
    private CallOutcome()
    {
    }

    public sealed record Body(string Json) : CallOutcome;

    public sealed record Failed(MoveFailure Failure) : CallOutcome;
}
