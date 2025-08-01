using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.Queue;

namespace Orpheus.Commands;

public class PlayNext : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly ISongQueueService _queueService;
    private readonly IQueuePlaybackService _queuePlaybackService;
    private readonly ILogger<PlayNext> _logger;

    public PlayNext(
        ISongQueueService queueService,
        IQueuePlaybackService queuePlaybackService,
        ILogger<PlayNext> logger)
    {
        _queueService = queueService;
        _queuePlaybackService = queuePlaybackService;
        _logger = logger;
    }

    [SlashCommand("playnext", "Add a YouTube video to the front of the queue (plays next).", Contexts = [InteractionContextType.Guild])]
    public async Task Command(string url)
    {
        var guild = Context.Guild!;
        var client = Context.Client;
        var userId = Context.User.Id;

        _logger.LogInformation("Received /playnext command for URL: {Url} from user {UserId} in guild {GuildId}", url, userId, guild.Id);

        try
        {
            // Extract title from URL for display (simplified)
            var title = ExtractTitleFromUrl(url);
            
            // Create queued song and add to front of queue
            var queuedSong = new QueuedSong(title, url, userId);
            _queueService.EnqueueSongNext(queuedSong);

            var message = _queueService.CurrentSong == null
                ? $"Added **{title}** to queue and starting playback!"
                : $"Added **{title}** to play next!";

            await RespondAsync(InteractionCallback.Message(message));

            // Start queue processing if not already running
            if (!_queuePlaybackService.IsProcessing)
            {
                await _queuePlaybackService.StartQueueProcessingAsync(guild, client, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in /playnext command for URL: {Url}", url);
            await RespondAsync(InteractionCallback.Message("An error occurred while adding the song to the queue."));
        }
    }

    private static string ExtractTitleFromUrl(string url)
    {
        // Simple extraction - in a real implementation you might want to fetch metadata
        if (url.Contains("youtube.com") || url.Contains("youtu.be"))
        {
            return "YouTube Video";
        }
        return "Audio Track";
    }
}