using Microsoft.Extensions.Logging;
using Orpheus.Services.Cache;
using System.Text.RegularExpressions;

namespace Orpheus.Services.Downloader.Youtube;

public class CachedYouTubeDownloaderService : IYouTubeDownloader
{
    private readonly IYouTubeDownloader _baseDownloader;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedYouTubeDownloaderService> _logger;
    private static readonly Regex YouTubeVideoIdRegex = new(@"(?:youtube\.com/watch\?v=|youtu\.be/|youtube\.com/embed/)([a-zA-Z0-9_-]{11})", RegexOptions.Compiled);

    public CachedYouTubeDownloaderService(
        IYouTubeDownloader baseDownloader,
        ICacheService cacheService,
        ILogger<CachedYouTubeDownloaderService> logger)
    {
        _baseDownloader = baseDownloader;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<string?> DownloadAsync(string url)
    {
        var uniqueId = ExtractUniqueId(url);
        if (string.IsNullOrEmpty(uniqueId))
        {
            _logger.LogWarning("Could not extract unique ID from URL: {Url}", url);
            // Fall back to base downloader for non-YouTube URLs or invalid formats
            return await _baseDownloader.DownloadAsync(url);
        }

        _logger.LogDebug("Checking cache for video ID: {UniqueId}", uniqueId);

        // Check if already cached
        var cachedSong = await _cacheService.GetCachedSongAsync(uniqueId);
        if (cachedSong != null)
        {
            _logger.LogInformation("Cache hit! Using cached file for {Title} ({UniqueId})", cachedSong.Title, uniqueId);
            await _cacheService.UpdateLastAccessedAsync(uniqueId);
            return cachedSong.FilePath;
        }

        _logger.LogDebug("Cache miss for {UniqueId}, downloading...", uniqueId);

        // Not cached, download using base downloader
        var filePath = await _baseDownloader.DownloadAsync(url);
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.LogWarning("Download failed for URL: {Url}", url);
            return null;
        }

        // Get title for better cache metadata
        var title = await GetVideoTitleAsync(url) ?? "Unknown Title";

        // Add to cache
        var cacheSuccess = await _cacheService.AddToCacheAsync(uniqueId, title, url, filePath);
        if (cacheSuccess)
        {
            _logger.LogInformation("Successfully cached downloaded file: {Title} ({UniqueId})", title, uniqueId);
        }
        else
        {
            _logger.LogWarning("Failed to add downloaded file to cache: {FilePath}", filePath);
        }

        return filePath;
    }

    public async Task<string?> GetVideoTitleAsync(string url)
    {
        // Delegate to base downloader for title fetching
        return await _baseDownloader.GetVideoTitleAsync(url);
    }

    public async Task<string?> SearchAndGetFirstUrlAsync(string searchQuery)
    {
        // Delegate to base downloader for search functionality
        return await _baseDownloader.SearchAndGetFirstUrlAsync(searchQuery);
    }

    /// <summary>
    /// Extracts a unique identifier from a YouTube URL
    /// </summary>
    private string? ExtractUniqueId(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // Extract YouTube video ID using regex
        var match = YouTubeVideoIdRegex.Match(url);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // For other URLs, we could use the URL itself as the unique ID (hashed)
        // but for now, we'll only cache YouTube videos
        return null;
    }
}