using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.Queue;
using Orpheus.Services.Downloader.Youtube;

namespace Orpheus.Commands;

public class Play : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly ISongQueueService _queueService;
    private readonly IQueuePlaybackService _queuePlaybackService;
    private readonly IYouTubeDownloader _downloader;
    private readonly ILogger<Play> _logger;

    public Play(
        ISongQueueService queueService,
        IQueuePlaybackService queuePlaybackService,
        IYouTubeDownloader downloader,
        ILogger<Play> logger)
    {
        _queueService = queueService;
        _queuePlaybackService = queuePlaybackService;
        _downloader = downloader;
        _logger = logger;
    }

    [SlashCommand("play", "Add a YouTube video to the queue by URL.", Contexts = [InteractionContextType.Guild])]
    public async Task Command(string url)
    {
        var guild = Context.Guild!;
        var client = Context.Client;
        var userId = Context.User.Id;

        _logger.LogInformation("Received /play command for URL: {Url} from user {UserId} in guild {GuildId}", url, userId, guild.Id);

        try
        {
            // Use placeholder title for immediate response to avoid Discord timeout
            var placeholderTitle = GetPlaceholderTitle(url);
            
            // Check if queue was empty before adding
            var wasQueueEmpty = _queueService.IsEmpty && _queueService.CurrentSong == null;
            
            // Create queued song immediately with placeholder title
            var queuedSong = new QueuedSong(placeholderTitle, url, userId);
            _queueService.EnqueueSong(queuedSong);

            var queuePosition = _queueService.Count;
            var message = wasQueueEmpty
                ? $"Added **{placeholderTitle}** to queue and starting playback!" 
                : $"Added **{placeholderTitle}** to queue (position {queuePosition})";

            await RespondAsync(InteractionCallback.Message(message));

            // Auto-start queue processing if queue was empty (first song added)
            if (wasQueueEmpty || !_queuePlaybackService.IsProcessing)
            {
                await _queuePlaybackService.StartQueueProcessingAsync(guild, client, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in /play command for URL: {Url}", url);
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