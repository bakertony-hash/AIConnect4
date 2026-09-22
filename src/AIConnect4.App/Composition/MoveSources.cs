using AIConnect4.App.OpenRouter;
using AIConnect4.Core;

namespace AIConnect4.App.Composition;

/// <summary>Builds the closed-over <see cref="IMoveSource"/> for one side from its profile and chat knobs.</summary>
public static class MoveSources
{
    public static IMoveSource Create(OpenRouterClient client, ModelProfile profile, ChatTuning tuning) =>
        profile switch
        {
            ModelProfile.SystemOne systemOne => new JevMoveSource(client, systemOne),
            ModelProfile.Chat chat => new ChatMoveSource(client, chat, tuning),
            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
        };
}
