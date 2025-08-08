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

    public async Task<string?> SearchAndGetFirstUrlAsync(string searchQuery)
    {
        _logger.LogInformation("Searching YouTube for: {SearchQuery}", searchQuery);
        
        try
        {
            // Use ytsearch: prefix to search YouTube for the query and get the first result
            var searchUrl = $"ytsearch1:{searchQuery}";
            _logger.LogDebug("Using search URL: {SearchUrl}", searchUrl);
            
            // Try to get video info first
            var result = await _youtubeDl.RunVideoDataFetch(searchUrl);
            
            if (!result.Success)
            {
                _logger.LogWarning("Failed to search for query: {SearchQuery}. Error: {Error}", 
                    searchQuery, string.Join(", ", result.ErrorOutput));
                return null;
            }

            // Enhanced logging to debug the search result data
            _logger.LogInformation("Search result for '{SearchQuery}': Success={Success}, HasData={HasData}", 
                searchQuery, result.Success, result.Data != null);
            
            if (result.Data != null)
            {
                _logger.LogInformation("Search result data - WebpageUrl: {WebpageUrl}, Id: {Id}, Title: {Title}", 
                    result.Data.WebpageUrl ?? "null",
                    result.Data.ID ?? "null", 
                    result.Data.Title ?? "null");
            }

            // Construct YouTube URL from video ID if available - with validation
            if (!string.IsNullOrWhiteSpace(result.Data?.ID))
            {
                var videoId = result.Data.ID.Trim();
                
                // Validate video ID format (YouTube video IDs are typically 11 characters, alphanumeric + - _)
                if (videoId.Length == 11 && videoId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                {
                    var constructedUrl = $"https://www.youtube.com/watch?v={videoId}";
                    _logger.LogInformation("✅ Constructed valid YouTube URL from video ID for '{SearchQuery}': {Url}", searchQuery, constructedUrl);
                    return constructedUrl;
                }
                else
                {
                    _logger.LogWarning("⚠️ Invalid video ID format '{VideoId}' for search query '{SearchQuery}' - length: {Length}", 
                        videoId, searchQuery, videoId.Length);
                }
            }

            // Fallback to WebpageUrl if video ID construction failed
            var webpageUrl = result.Data?.WebpageUrl?.Trim();
            if (!string.IsNullOrWhiteSpace(webpageUrl))
            {
                // Validate that it's actually a YouTube URL
                if (webpageUrl.Contains("youtube.com/watch") || webpageUrl.Contains("youtu.be/"))
                {
                    _logger.LogInformation("✅ Using WebpageUrl for search query '{SearchQuery}': {Url}", searchQuery, webpageUrl);
                    return webpageUrl;
                }
                else
                {
                    _logger.LogWarning("⚠️ WebpageUrl is not a valid YouTube URL for search query '{SearchQuery}': {Url}", searchQuery, webpageUrl);
                }
            }

            _logger.LogError("❌ No valid YouTube URL found for search query '{SearchQuery}' - both video ID and WebpageUrl were invalid or missing", searchQuery);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while searching for query: {SearchQuery}", searchQuery);
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