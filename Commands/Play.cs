using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services;
using Orpheus.Services.Queue;
using Orpheus.Services.Downloader.Youtube;

namespace Orpheus.Commands;

public class Play : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly ISongQueueService _queueService;
    private readonly IQueuePlaybackService _queuePlaybackService;
    private readonly IYouTubeDownloader _downloader;
    private readonly IMessageUpdateService _messageUpdateService;
    private readonly ILogger<Play> _logger;

    public Play(
        ISongQueueService queueService,
        IQueuePlaybackService queuePlaybackService,
        IYouTubeDownloader downloader,
        IMessageUpdateService messageUpdateService,
        ILogger<Play> logger)
    {
        _queueService = queueService;
        _queuePlaybackService = queuePlaybackService;
        _downloader = downloader;
        _messageUpdateService = messageUpdateService;
        _logger = logger;
    }

    [SlashCommand("play", "Add a YouTube video to the queue by URL or search query.", Contexts = [InteractionContextType.Guild])]
    public async Task Command(string query)
    {
        var guild = Context.Guild!;
        var client = Context.Client;
        var userId = Context.User.Id;

        _logger.LogInformation("Received /play command for query: {Query} from user {UserId} in guild {GuildId}", query, userId, guild.Id);

        try
        {
            string? url = null;
            string placeholderTitle;
            
            // Check if the input is a URL or a search query
            if (IsUrl(query))
            {
                url = query;
                placeholderTitle = GetPlaceholderTitle(url);
                _logger.LogDebug("Input detected as URL: {Url}", url);
            }
            else
            {
                // It's a search query - search for the video
                placeholderTitle = $"Searching for: {query}";
                _logger.LogDebug("Input detected as search query: {Query}", query);
                
                // Search for the first result
                url = await _downloader.SearchAndGetFirstUrlAsync(query);
                if (url == null)
                {
                    await RespondAsync(InteractionCallback.Message($"No results found for: **{query}**"));
                    return;
                }
                
                _logger.LogInformation("Search found URL: {Url} for query: {Query}", url, query);
                placeholderTitle = $"Found: {query}"; // Will be updated with real title
            }
            
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
            _logger.LogError(ex, "Error in /play command for query: {Query}", query);
            await RespondAsync(InteractionCallback.Message("An error occurred while adding the song to the queue."));
        }
    }

    private static bool IsUrl(string input)
    {
        return Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
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