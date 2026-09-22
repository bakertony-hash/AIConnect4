using System.Net.Http.Headers;

namespace AIConnect4.App.OpenRouter;

/// <summary>The one <see cref="HttpClient"/> both adapters share. The composition root creates it once and never disposes it mid-series.</summary>
public static class OpenRouterHttp
{
    public static Uri BaseAddress { get; } = new("https://openrouter.ai/api/");

    public const string ApiKeyVariable = "OPENROUTER_API_KEY";

    /// <summary>Null when the variable is unset or blank, which leaves the app in Ready with Play disabled.</summary>
    public static string? ApiKeyFromEnvironment()
    {
        var key = Environment.GetEnvironmentVariable(ApiKeyVariable);
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    /// <summary>Tests pass a fake <paramref name="handler"/>. The per-call timeout lives in <see cref="OpenRouterClient"/>, so the client's own is off.</summary>
    public static HttpClient Create(string apiKey, HttpMessageHandler? handler = null)
    {
        var http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.BaseAddress = BaseAddress;
        http.Timeout = Timeout.InfiniteTimeSpan;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        http.DefaultRequestHeaders.Add("X-Title", "AI Connect 4");
        return http;
    }
}
