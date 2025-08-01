using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.Queue;

namespace Orpheus.Commands;

public class ClearQueue : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly ISongQueueService _queueService;
    private readonly IQueuePlaybackService _queuePlaybackService;
    private readonly ILogger<ClearQueue> _logger;

    public ClearQueue(ISongQueueService queueService, IQueuePlaybackService queuePlaybackService, ILogger<ClearQueue> logger)
    {
        _queueService = queueService;
        _queuePlaybackService = queuePlaybackService;
        _logger = logger;
    }

    [SlashCommand("clearqueue", "Clear all songs from the queue and stop playback.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        _logger.LogInformation("Received /clearqueue command from user {UserId}", Context.User.Id);

        var queueCount = _queueService.Count;
        var currentSong = _queueService.CurrentSong;

        if (queueCount == 0 && currentSong == null)
        {
            await RespondAsync(InteractionCallback.Message("The queue is already empty."));
            return;
        }

        try
        {
            await _queuePlaybackService.StopQueueProcessingAsync();
            _queueService.ClearQueue();
            
            var message = currentSong != null 
                ? $"🗑️ Cleared queue and stopped playback. Removed {queueCount} songs from queue." 
                : $"🗑️ Cleared {queueCount} songs from queue.";
                
            await RespondAsync(InteractionCallback.Message(message));
            _logger.LogInformation("Cleared queue with {Count} songs", queueCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing queue");
            await RespondAsync(InteractionCallback.Message("Failed to clear the queue."));
        }
    }
}