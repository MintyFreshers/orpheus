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
    private readonly ILogger<BackgroundDownloadService> _logger;
    private readonly HashSet<string> _downloadingUrls = new();
    private readonly object _downloadingLock = new();

    public BackgroundDownloadService(
        ISongQueueService queueService,
        IYouTubeDownloader downloader,
        ILogger<BackgroundDownloadService> logger)
    {
        _queueService = queueService;
        _downloader = downloader;
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
        
        // Process current song first if it needs downloading
        if (currentSong != null && NeedsDownload(currentSong))
        {
            await DownloadSongAsync(currentSong, cancellationToken);
        }

        // Then process songs in queue
        foreach (var song in queue)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (NeedsDownload(song))
            {
                await DownloadSongAsync(song, cancellationToken);
            }
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

    private async Task DownloadSongAsync(QueuedSong song, CancellationToken cancellationToken)
    {
        lock (_downloadingLock)
        {
            if (_downloadingUrls.Contains(song.Url))
                return;
            _downloadingUrls.Add(song.Url);
        }

        try
        {
            _logger.LogInformation("Background downloading: {Title}", song.Title);
            
            // Update title if it's still generic
            if (song.Title == "YouTube Video" || song.Title == "Audio Track")
            {
                var actualTitle = await _downloader.GetVideoTitleAsync(song.Url);
                if (!string.IsNullOrWhiteSpace(actualTitle))
                {
                    song.Title = actualTitle;
                    _logger.LogDebug("Updated title for {Url}: {Title}", song.Url, actualTitle);
                }
            }

            var filePath = await _downloader.DownloadAsync(song.Url);
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                song.FilePath = filePath;
                _logger.LogInformation("Background download completed: {Title}", song.Title);
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
            lock (_downloadingLock)
            {
                _downloadingUrls.Remove(song.Url);
            }
        }
    }
}