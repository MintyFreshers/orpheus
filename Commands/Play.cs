using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.Downloader.Youtube;
using Orpheus.Services.Queue;

namespace Orpheus.Commands;

public class Play : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IYouTubeDownloader _downloader;
    private readonly ISongQueueService _queueService;
    private readonly IQueuePlaybackService _queuePlaybackService;
    private readonly ILogger<Play> _logger;

    public Play(
        IYouTubeDownloader downloader,
        ISongQueueService queueService,
        IQueuePlaybackService queuePlaybackService,
        ILogger<Play> logger)
    {
        _downloader = downloader;
        _queueService = queueService;
        _queuePlaybackService = queuePlaybackService;
        _logger = logger;
    }

    [SlashCommand("play", "Download a YouTube video by URL and add it to the queue.", Contexts = [InteractionContextType.Guild])]
    public async Task Command(string url)
    {
        var guild = Context.Guild!;
        var client = Context.Client;
        var userId = Context.User.Id;

        _logger.LogInformation("Received /play command for URL: {Url} from user {UserId} in guild {GuildId}", url, userId, guild.Id);

        try
        {
            await RespondAsync(InteractionCallback.Message("Downloading audio and adding to queue..."));
            var filePath = await _downloader.DownloadAsync(url);

            _logger.LogInformation("DownloadAsync returned filePath: '{FilePath}'", filePath);

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                var normalizedPath = Path.GetFullPath(filePath.Trim());
                _logger.LogInformation("Normalized filePath: '{NormalizedPath}'", normalizedPath);
                filePath = normalizedPath;
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                _logger.LogWarning("Download failed or file not found for URL: {Url}. filePath: '{FilePath}'", url, filePath);
                await Context.Interaction.SendFollowupMessageAsync("Failed to download the audio. Please check the URL and try again.");
                return;
            }

            _logger.LogInformation("Downloaded file: {FilePath}", filePath);

            // Extract title from filename for display
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var queuedSong = new QueuedSong(fileName, url, filePath, userId);
            
            _queueService.EnqueueSong(queuedSong);

            var queuePosition = _queueService.Count;
            var message = queuePosition == 1 
                ? $"Added **{fileName}** to queue and starting playback!" 
                : $"Added **{fileName}** to queue (position {queuePosition})";

            await Context.Interaction.SendFollowupMessageAsync(message);

            // Start queue processing if not already running
            if (!_queuePlaybackService.IsProcessing)
            {
                await _queuePlaybackService.StartQueueProcessingAsync(guild, client, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in /play command for URL: {Url}", url);
            await Context.Interaction.SendFollowupMessageAsync("An error occurred while processing your request.");
        }
    }
}