using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace Orpheus.Services.Downloader.Youtube;

public class YouTubeDownloaderService : IYouTubeDownloader
{
    private readonly YoutubeDL _youtubeDl;
    private readonly ILogger<YouTubeDownloaderService> _logger;
    private readonly string _downloadFolder;

    public YouTubeDownloaderService(
        ILogger<YouTubeDownloaderService> logger,
        ILogger<VerboseYoutubeDL> verboseLogger)
    {
        _logger = logger;
        _downloadFolder = Path.Combine(Directory.GetCurrentDirectory(), "Downloads");
        
        Directory.CreateDirectory(_downloadFolder);
        
#if DEBUG
        _youtubeDl = new VerboseYoutubeDL(verboseLogger);
        _logger.LogDebug("Using VerboseYoutubeDL (DEBUG build)");
#else
        _youtubeDl = new YoutubeDL();
        _logger.LogDebug("Using standard YoutubeDL (RELEASE build)");
#endif
        
        _youtubeDl.OutputFolder = _downloadFolder;
        _logger.LogDebug("Download folder set to: {DownloadFolder}", _downloadFolder);
        
        LogBinaryPaths();
    }

    public async Task<string?> DownloadAsync(string url)
    {
        _logger.LogInformation("Starting download for URL: {Url}", url);
        
        var filesBefore = GetExistingMp3Files();
        var result = await _youtubeDl.RunAudioDownload(url, AudioConversionFormat.Mp3);

        if (!result.Success)
        {
            LogDownloadFailure(url, result);
            return null;
        }

        return FindDownloadedFile(filesBefore, result, url);
    }

    public async Task<string?> GetVideoTitleAsync(string url)
    {
        _logger.LogDebug("Getting video title for URL: {Url}", url);
        
        try
        {
            var result = await _youtubeDl.RunVideoDataFetch(url);
            
            if (!result.Success)
            {
                _logger.LogWarning("Failed to fetch video data for URL: {Url}. Error: {Error}", 
                    url, string.Join(", ", result.ErrorOutput));
                return null;
            }

            var title = result.Data?.Title;
            if (!string.IsNullOrWhiteSpace(title))
            {
                _logger.LogDebug("Retrieved title for URL {Url}: {Title}", url, title);
                return title;
            }

            _logger.LogWarning("Video data retrieved but title was empty for URL: {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching video title for URL: {Url}", url);
            return null;
        }
    }

    private HashSet<string> GetExistingMp3Files()
    {
        return Directory.GetFiles(_downloadFolder, "*.mp3").ToHashSet();
    }

    private string? FindDownloadedFile(HashSet<string> filesBefore, RunResult<string> result, string url)
    {
        var newFileFromDisk = FindNewlyCreatedFile(filesBefore);
        if (newFileFromDisk != null)
        {
            _logger.LogInformation("Download complete. New file found: {File}", newFileFromDisk);
            return newFileFromDisk;
        }

        var fileFromResult = GetFileFromResultData(result);
        if (fileFromResult != null)
        {
            _logger.LogInformation("Download complete. File found via result data: {File}", fileFromResult);
            return fileFromResult;
        }

        _logger.LogWarning("Download reported success, but no new file was found for URL: {Url}", url);
        return null;
    }

    private string? FindNewlyCreatedFile(HashSet<string> filesBefore)
    {
        var filesAfter = Directory.GetFiles(_downloadFolder, "*.mp3");
        var newFiles = filesAfter.Where(f => !filesBefore.Contains(f)).ToArray();
        
        if (newFiles.Length == 0)
        {
            return null;
        }

        return newFiles
            .Select(f => new FileInfo(f))
            .OrderByDescending(fi => fi.CreationTime)
            .First()
            .FullName;
    }

    private string? GetFileFromResultData(RunResult<string> result)
    {
        if (string.IsNullOrWhiteSpace(result.Data) || !File.Exists(result.Data))
        {
            return null;
        }

        return Path.GetFullPath(result.Data);
    }

    private void LogDownloadFailure(string url, RunResult<string> result)
    {
        _logger.LogError("Download failed for URL: {Url}. Error: {Error}", 
            url, 
            string.Join(", ", result.ErrorOutput));
    }

    private void LogBinaryPaths()
    {
        _logger.LogDebug("yt-dlp path: {Path}, exists: {Exists}", 
            _youtubeDl.YoutubeDLPath, 
            File.Exists(_youtubeDl.YoutubeDLPath));
            
        _logger.LogDebug("ffmpeg path: {Path}, exists: {Exists}", 
            _youtubeDl.FFmpegPath, 
            File.Exists(_youtubeDl.FFmpegPath));
    }
}