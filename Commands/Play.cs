using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.Downloader.Youtube;
using Orpheus.Services.VoiceClientController;

namespace Orpheus.Commands;

public class Play : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IYouTubeDownloader _downloader;
    private readonly IVoiceClientController _voiceClientController;
    private readonly ILogger<Play> _logger;

    public Play(IYouTubeDownloader downloader, IVoiceClientController voiceClientController, ILogger<Play> logger)
    {
        _downloader = downloader;
        _voiceClientController = voiceClientController;
        _logger = logger;
    }

    [SlashCommand("play", "Download a YouTube video by URL and play it in your voice channel.", Contexts = [InteractionContextType.Guild])]
    public async Task Command(string url)
    {
        var guild = Context.Guild!;
        var client = Context.Client;
        var userId = Context.User.Id;

        _logger.LogInformation("Received /play command for URL: {Url} from user {UserId} in guild {GuildId}", url, userId, guild.Id);

        try
        {
            await RespondAsync(InteractionCallback.Message("Downloading audio (any previous playback will be stopped)..."));
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
                return;
            }

            _logger.LogInformation("Downloaded file: {FilePath}", filePath);

            var resultMessage = await _voiceClientController.PlayMp3Async(guild, client, userId, filePath);

            if (resultMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Failed to play MP3: {Message}", resultMessage);
            }
            else
            {
                _logger.LogInformation("Play result: {Message}", resultMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in /play command for URL: {Url}", url);
        }
    }
}