using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orpheus.Services.Downloader.Youtube;

namespace Orpheus.Services.Queue;

public interface IBackgroundDownloadService
{
    Task StartAsync();
    Task StopAsync();
}

public class BackgroundDownloadService : BackgroundService, IBackgroundDownloadService
{
    private readonly ISongQueueService _queueService;
    private readonly IYouTubeDownloader _downloader;
    private readonly IFollowUpMessageService _followUpMessageService;
    private readonly ILogger<BackgroundDownloadService> _logger;
    private readonly HashSet<string> _downloadingUrls = new();
    private readonly HashSet<string> _fetchingMetadataUrls = new();
    private readonly object _downloadingLock = new();
    private readonly object _metadataLock = new();
    private readonly SemaphoreSlim _downloadSemaphore = new(3); // Max 3 concurrent downloads
    private readonly SemaphoreSlim _metadataSemaphore = new(5); // Max 5 concurrent metadata fetches

    public BackgroundDownloadService(
        ISongQueueService queueService,
        IYouTubeDownloader downloader,
        IFollowUpMessageService followUpMessageService,
        ILogger<BackgroundDownloadService> logger)
    {
        _queueService = queueService;
        _downloader = downloader;
        _followUpMessageService = followUpMessageService;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        await base.StartAsync(CancellationToken.None);
    }

    public async Task StopAsync()
    {
        await base.StopAsync(CancellationToken.None);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background download service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueDownloads(stoppingToken);
                
                // Wait before next check to avoid excessive polling
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background download service");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Background download service stopped");
    }

    private async Task ProcessQueueDownloads(CancellationToken cancellationToken)
    {
        var queue = _queueService.GetQueue();
        var currentSong = _queueService.CurrentSong;
        
        var downloadTasks = new List<Task>();
        var metadataTasks = new List<Task>();
        
        // Process current song first if it needs downloading or metadata
        if (currentSong != null)
        {
            if (NeedsDownload(currentSong))
            {
                downloadTasks.Add(DownloadSongAsync(currentSong, cancellationToken));
            }
            else if (NeedsMetadata(currentSong))
            {
                metadataTasks.Add(FetchMetadataAsync(currentSong, cancellationToken));
            }
        }

        // Then process songs in queue
        foreach (var song in queue)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (NeedsDownload(song))
            {
                downloadTasks.Add(DownloadSongAsync(song, cancellationToken));
            }
            else if (NeedsMetadata(song))
            {
                metadataTasks.Add(FetchMetadataAsync(song, cancellationToken));
            }
        }

        // Start all tasks concurrently
        var allTasks = downloadTasks.Concat(metadataTasks);
        if (allTasks.Any())
        {
            await Task.WhenAll(allTasks);
        }
    }

    private bool NeedsDownload(QueuedSong song)
    {
        // Don't download if already downloaded
        if (!string.IsNullOrWhiteSpace(song.FilePath) && File.Exists(song.FilePath))
            return false;

        // Don't download if already downloading
        lock (_downloadingLock)
        {
            return !_downloadingUrls.Contains(song.Url);
        }
    }

    private bool NeedsMetadata(QueuedSong song)
    {
        // Need metadata if title is still generic
        if (song.Title != "YouTube Video" && song.Title != "Audio Track")
            return false;

        // Don't fetch if already fetching
        lock (_metadataLock)
        {
            return !_fetchingMetadataUrls.Contains(song.Url);
        }
    }

    private async Task FetchMetadataAsync(QueuedSong song, CancellationToken cancellationToken)
    {
        // Check if we should fetch metadata
        lock (_metadataLock)
        {
            if (_fetchingMetadataUrls.Contains(song.Url))
                return;
            _fetchingMetadataUrls.Add(song.Url);
        }

        await _metadataSemaphore.WaitAsync(cancellationToken);
        
        try
        {
            _logger.LogDebug("Fetching metadata for: {Url}", song.Url);
            
            var actualTitle = await _downloader.GetVideoTitleAsync(song.Url);
            if (!string.IsNullOrWhiteSpace(actualTitle))
            {
                song.Title = actualTitle;
                _logger.LogDebug("Updated title for {Url}: {Title}", song.Url, actualTitle);
                
                // Send follow-up message with real title
                await _followUpMessageService.SendSongTitleUpdateAsync(song.Id, actualTitle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching metadata for song: {Url}", song.Url);
        }
        finally
        {
            _metadataSemaphore.Release();
            lock (_metadataLock)
            {
                _fetchingMetadataUrls.Remove(song.Url);
            }
        }
    }

    private async Task DownloadSongAsync(QueuedSong song, CancellationToken cancellationToken)
    {
        // Check if we should download
        lock (_downloadingLock)
        {
            if (_downloadingUrls.Contains(song.Url))
                return;
            _downloadingUrls.Add(song.Url);
        }

        await _downloadSemaphore.WaitAsync(cancellationToken);

        try
        {
            _logger.LogDebug("Background downloading: {Title}", song.Title);
            
            var filePath = await _downloader.DownloadAsync(song.Url);
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                song.FilePath = filePath;
                _logger.LogDebug("Background download completed: {Title}", song.Title);
            }
            else
            {
                _logger.LogWarning("Background download failed: {Title}", song.Title);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading song: {Title}", song.Title);
        }
        finally
        {
            _downloadSemaphore.Release();
            lock (_downloadingLock)
            {
                _downloadingUrls.Remove(song.Url);
            }
        }
    }

    public override void Dispose()
    {
        _downloadSemaphore?.Dispose();
        _metadataSemaphore?.Dispose();
        base.Dispose();
    }
}