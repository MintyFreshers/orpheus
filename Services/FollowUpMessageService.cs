using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;

namespace Orpheus.Services;

public interface IFollowUpMessageService
{
    Task RegisterInteractionForSongUpdatesAsync(ulong interactionId, ApplicationCommandInteraction interaction, string songId);
    Task SendSongTitleUpdateAsync(string songId, string actualTitle);
    void RemoveInteraction(ulong interactionId);
}

public class FollowUpMessageService : IFollowUpMessageService
{
    private readonly ILogger<FollowUpMessageService> _logger;
    private readonly Dictionary<string, List<InteractionContext>> _songInteractionMap = new();
    private readonly object _lock = new();

    public FollowUpMessageService(ILogger<FollowUpMessageService> logger)
    {
        _logger = logger;
    }

    public async Task RegisterInteractionForSongUpdatesAsync(ulong interactionId, ApplicationCommandInteraction interaction, string songId)
    {
        var context = new InteractionContext(interactionId, interaction);
        
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
                var followUpMessage = $"🎵 **{actualTitle}** has been added to the queue!";
                await context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties
                {
                    Content = followUpMessage
                });
                
                _logger.LogDebug("Sent follow-up message for song: {Title}", actualTitle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send follow-up message for interaction {InteractionId}", context.InteractionId);
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

    private record InteractionContext(ulong InteractionId, ApplicationCommandInteraction Interaction);
}