using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;

namespace Orpheus.Services;

public interface IMessageUpdateService
{
    Task RegisterInteractionForSongUpdatesAsync(ulong interactionId, ApplicationCommandInteraction interaction, string songId, string originalMessage, bool isDeferred = false);
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

    public async Task RegisterInteractionForSongUpdatesAsync(ulong interactionId, ApplicationCommandInteraction interaction, string songId, string originalMessage, bool isDeferred = false)
    {
        var context = new InteractionContext(interactionId, interaction, originalMessage, isDeferred);
        
        lock (_lock)
        {
            if (!_songInteractionMap.ContainsKey(songId))
            {
                _songInteractionMap[songId] = new List<InteractionContext>();
            }
            
            _songInteractionMap[songId].Add(context);
        }

        _logger.LogDebug("Registered interaction {InteractionId} for song updates: {SongId}, deferred: {IsDeferred}", interactionId, songId, isDeferred);
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
                // Update the message content with actual title
                var originalContent = context.OriginalMessage ?? string.Empty;
                var updatedContent = originalContent;
                
                // Replace placeholder text with actual title
                if (originalContent.Contains("YouTube Video"))
                {
                    updatedContent = originalContent.Replace("YouTube Video", actualTitle);
                }
                else if (originalContent.Contains("Found: "))
                {
                    // For search queries, replace the search term with actual title
                    var foundIndex = originalContent.IndexOf("Found: ");
                    if (foundIndex >= 0)
                    {
                        var beforeFound = originalContent.Substring(0, foundIndex + 7); // "Found: "
                        var afterFound = originalContent.Substring(foundIndex + 7);
                        
                        // Find the end of the title (before "** to queue")
                        var endIndex = afterFound.IndexOf("** to queue");
                        if (endIndex >= 0)
                        {
                            var afterTitle = afterFound.Substring(endIndex);
                            updatedContent = beforeFound + actualTitle + afterTitle;
                        }
                        else
                        {
                            // Fallback - just replace everything after "Found: "
                            updatedContent = beforeFound + actualTitle + "** to queue and starting playback!";
                        }
                    }
                }
                
                // Update the original response
                await context.Interaction.ModifyResponseAsync(properties =>
                {
                    properties.Content = updatedContent;
                });
                
                _logger.LogDebug("Updated message with real song title: {Title}, deferred: {IsDeferred}", actualTitle, context.IsDeferred);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update message for interaction {InteractionId}: {Error}", context.InteractionId, ex.Message);
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

    private record InteractionContext(ulong InteractionId, ApplicationCommandInteraction Interaction, string? OriginalMessage, bool IsDeferred = false);
}