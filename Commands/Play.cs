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
            // Extract title from URL for display
            var title = await ExtractTitleFromUrlAsync(url, _downloader);
            
            // Create queued song immediately without downloading
            var queuedSong = new QueuedSong(title, url, userId);
            _queueService.EnqueueSong(queuedSong);

            var queuePosition = _queueService.Count;
            var message = queuePosition == 1 && _queueService.CurrentSong == null
                ? $"Added **{title}** to queue and starting playback!" 
                : $"Added **{title}** to queue (position {queuePosition})";

            await RespondAsync(InteractionCallback.Message(message));

            // Start queue processing if not already running
            if (!_queuePlaybackService.IsProcessing)
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

    private static async Task<string> ExtractTitleFromUrlAsync(string url, IYouTubeDownloader downloader)
    {
        // Try to get the actual title from YouTube
        if (url.Contains("youtube.com") || url.Contains("youtu.be"))
        {
            var title = await downloader.GetVideoTitleAsync(url);
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }
            return "YouTube Video"; // Fallback if title fetch fails
        }
        return "Audio Track";
    }
}