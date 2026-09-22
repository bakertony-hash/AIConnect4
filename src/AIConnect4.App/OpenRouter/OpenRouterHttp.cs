using System.Net.Http.Headers;

namespace AIConnect4.App.OpenRouter;

public static class OpenRouterHttp
{
    public static Uri BaseAddress { get; } = new("https://openrouter.ai/api/");

    public const string ApiKeyVariable = "OPENROUTER_API_KEY";

    /// <summary>Null when the variable is unset or blank.</summary>
    public static string? ApiKeyFromEnvironment()
    {
        var key = Environment.GetEnvironmentVariable(ApiKeyVariable);
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    /// <summary>The returned client has no timeout of its own; <see cref="OpenRouterClient"/> applies the per-call timeout.</summary>
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
