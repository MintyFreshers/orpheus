using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;

namespace Orpheus.Services;

public interface IMessageUpdateService
{
    Task RegisterInteractionForSongUpdatesAsync(ulong interactionId, ApplicationCommandInteraction interaction, string songId, string originalMessage);
    Task SendSongTitleUpdateAsync(string songId, string actualTitle);
    void RemoveInteraction(ulong interactionId);
}

public class MessageUpdateService : IMessageUpdateService
{
    private readonly ILogger<MessageUpdateService> _logger;
    private readonly Dictionary<string, List<InteractionContext>> _songInteractionMap = new();
    private readonly object _lock = new();

    public MessageUpdateService(ILogger<MessageUpdateService> logger)
    {
        _logger = logger;
    }

    public async Task RegisterInteractionForSongUpdatesAsync(ulong interactionId, ApplicationCommandInteraction interaction, string songId, string originalMessage)
    {
        var context = new InteractionContext(interactionId, interaction, originalMessage);
        
        lock (_lock)
        {
            if (!_songInteractionMap.ContainsKey(songId))
            {
                _songInteractionMap[songId] = new List<InteractionContext>();
            }
            
            _songInteractionMap[songId].Add(context);
        }

        _logger.LogDebug("Registered interaction {InteractionId} for song updates: {SongId}", interactionId, songId);
        await Task.CompletedTask;
    }

    public async Task SendSongTitleUpdateAsync(string songId, string actualTitle)
    {
        List<InteractionContext>? interactions;
        
        lock (_lock)
        {
            if (!_songInteractionMap.TryGetValue(songId, out interactions) || interactions == null)
            {
                return; // No interactions waiting for this song
            }
            
            // Remove the song from the map since we're sending the update
            _songInteractionMap.Remove(songId);
        }

        foreach (var context in interactions)
        {
            try
            {
                // Update the original message instead of sending a follow-up
                await context.Interaction.ModifyResponseAsync(properties =>
                {
                    // Replace "YouTube Video" placeholder with actual title in the original message
                    var originalContent = context.OriginalMessage ?? string.Empty;
                    var updatedContent = originalContent.Replace("YouTube Video", actualTitle);
                    properties.Content = updatedContent;
                });
                
                _logger.LogDebug("Updated original message with real song title: {Title}", actualTitle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update original message for interaction {InteractionId}", context.InteractionId);
            }
        }
    }

    public void RemoveInteraction(ulong interactionId)
    {
        lock (_lock)
        {
            foreach (var kvp in _songInteractionMap.ToList())
            {
                kvp.Value.RemoveAll(ctx => ctx.InteractionId == interactionId);
                if (kvp.Value.Count == 0)
                {
                    _songInteractionMap.Remove(kvp.Key);
                }
            }
        }
    }

    private record InteractionContext(ulong InteractionId, ApplicationCommandInteraction Interaction, string? OriginalMessage);
}