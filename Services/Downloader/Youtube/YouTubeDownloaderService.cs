using YoutubeDLSharp;
using Microsoft.Extensions.Logging;
using YoutubeDLSharp.Options;

namespace Orpheus.Services.Downloader.Youtube;

public class YouTubeDownloaderService : IYouTubeDownloader
{
    private readonly YoutubeDL _ytdl;
    private readonly ILogger<YouTubeDownloaderService> _logger;

    public YouTubeDownloaderService(ILogger<YouTubeDownloaderService> logger)
    {
        _logger = logger;
#if DEBUG
        _ytdl = new VerboseYoutubeDL(msg => _logger.LogInformation(msg));
        _logger.LogDebug("Using VerboseYoutubeDL (DEBUG build)");
#else
        _ytdl = new YoutubeDL();
        _logger.LogDebug("Using standard YoutubeDL (RELEASE build)");
#endif

        _logger.LogDebug("yt-dlp path: {Path}, exists: {Exists}", _ytdl.YoutubeDLPath, File.Exists(_ytdl.YoutubeDLPath));
        _logger.LogDebug("ffmpeg path: {Path}, exists: {Exists}", _ytdl.FFmpegPath, File.Exists(_ytdl.FFmpegPath));
    }

    public async Task<string> DownloadAsync(string url)
    {
        _logger.LogInformation("Starting download for URL: {Url}", url);
        var result = await _ytdl.RunAudioDownload(url, AudioConversionFormat.Mp3);
        if (result.Success && !string.IsNullOrWhiteSpace(result.Data))
        {
            _logger.LogInformation("Download complete. File saved to: {File}", result.Data);
            return $"Download complete. File saved to: {result.Data}";
        }
        else if (result.Success)
        {
            _logger.LogWarning("Download reported success, but no file path was returned for URL: {Url}", url);
            return "Download reported success, but no file path was returned.";
        }
        else
        {
            _logger.LogError("Download failed for URL: {Url}. Error: {Error}", url, string.Join(", ", result.ErrorOutput));
            return $"Download failed. {string.Join(", ", result.ErrorOutput)}";
        }
    }
}