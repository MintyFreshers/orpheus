using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services;
using Orpheus.Services.Queue;
using Orpheus.Services.Downloader.Youtube;

namespace Orpheus.Commands;

public class PlayNext : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly ISongQueueService _queueService;
    private readonly IQueuePlaybackService _queuePlaybackService;
    private readonly IYouTubeDownloader _downloader;
    private readonly IMessageUpdateService _messageUpdateService;
    private readonly ILogger<PlayNext> _logger;

    public PlayNext(
        ISongQueueService queueService,
        IQueuePlaybackService queuePlaybackService,
        IYouTubeDownloader downloader,
        IMessageUpdateService messageUpdateService,
        ILogger<PlayNext> logger)
    {
        _queueService = queueService;
        _queuePlaybackService = queuePlaybackService;
        _downloader = downloader;
        _messageUpdateService = messageUpdateService;
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
            // Use placeholder title for immediate response to avoid Discord timeout
            var placeholderTitle = GetPlaceholderTitle(url);
            
            // Check if queue was empty before adding
            var wasQueueEmpty = _queueService.IsEmpty && _queueService.CurrentSong == null;
            
            // Create queued song and add to front of queue
            var queuedSong = new QueuedSong(placeholderTitle, url, userId);
            _queueService.EnqueueSongNext(queuedSong);

            var message = wasQueueEmpty
                ? $"Added **{placeholderTitle}** to queue and starting playback!"
                : $"Added **{placeholderTitle}** to play next!";

            await RespondAsync(InteractionCallback.Message(message));

            // Register for original message updates when real title is fetched
            await _messageUpdateService.RegisterInteractionForSongUpdatesAsync(Context.Interaction.Id, Context.Interaction, queuedSong.Id, message);

            // Auto-start queue processing if queue was empty (first song added)
            if (wasQueueEmpty || !_queuePlaybackService.IsProcessing)
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

    private static string GetPlaceholderTitle(string url)
    {
        // Return immediate placeholder based on URL type - no async calls to avoid timeout
        if (url.Contains("youtube.com") || url.Contains("youtu.be"))
        {
            return "YouTube Video"; // Will be updated by background service
        }
        return "Audio Track";
    }
}