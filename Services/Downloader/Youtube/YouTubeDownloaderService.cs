using YoutubeDLSharp;
using Microsoft.Extensions.Logging;
using YoutubeDLSharp.Options;

namespace Orpheus.Services.Downloader.Youtube;

public class YouTubeDownloaderService : IYouTubeDownloader
{
    private readonly YoutubeDL _youtubeDl;
    private readonly ILogger<YouTubeDownloaderService> _logger;

    public YouTubeDownloaderService(
        ILogger<YouTubeDownloaderService> logger,
        ILogger<VerboseYoutubeDL> verboseLogger)
    {
        _logger = logger;
#if DEBUG
        _youtubeDl = new VerboseYoutubeDL(verboseLogger);
        _logger.LogDebug("Using VerboseYoutubeDL (DEBUG build)");
#else
        _youtubeDl = new YoutubeDL();
        _logger.LogDebug("Using standard YoutubeDL (RELEASE build)");
#endif
        LogBinaryPaths();
    }

    public async Task<string> DownloadAsync(string url)
    {
        _logger.LogInformation("Starting download for URL: {Url}", url);
        var result = await _youtubeDl.RunAudioDownload(url, AudioConversionFormat.Mp3);
        return GetDownloadResultMessage(result, url);
    }

    private void LogBinaryPaths()
    {
        _logger.LogDebug("yt-dlp path: {Path}, exists: {Exists}", _youtubeDl.YoutubeDLPath, File.Exists(_youtubeDl.YoutubeDLPath));
        _logger.LogDebug("ffmpeg path: {Path}, exists: {Exists}", _youtubeDl.FFmpegPath, File.Exists(_youtubeDl.FFmpegPath));
    }

    private string GetDownloadResultMessage(RunResult<string> result, string url)
    {
        if (result.Success && !string.IsNullOrWhiteSpace(result.Data))
        {
            _logger.LogInformation("Download complete. File saved to: {File}", result.Data);
            return $"Download complete. File saved to: {result.Data}";
        }
        if (result.Success)
        {
            _logger.LogWarning("Download reported success, but no file path was returned for URL: {Url}", url);
            return "Download reported success, but no file path was returned.";
        }
        _logger.LogError("Download failed for URL: {Url}. Error: {Error}", url, string.Join(", ", result.ErrorOutput));
        return $"Download failed. {string.Join(", ", result.ErrorOutput)}";
    }
}