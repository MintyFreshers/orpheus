using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using Orpheus.Services.Downloader.Youtube;
using Orpheus.Services.VoiceClientController;

namespace Orpheus.Services.Queue;

public interface IQueuePlaybackService
{
    Task StartQueueProcessingAsync(Guild guild, GatewayClient client, ulong userId);
    Task StopQueueProcessingAsync();
    Task SkipCurrentSongAsync();
    bool IsProcessing { get; }
}

public class QueuePlaybackService : IQueuePlaybackService
{
    private readonly ISongQueueService _queueService;
    private readonly IVoiceClientController _voiceClientController;
    private readonly IYouTubeDownloader _downloader;
    private readonly ILogger<QueuePlaybackService> _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _processingTask;
    private readonly object _lock = new();

    public QueuePlaybackService(
        ISongQueueService queueService,
        IVoiceClientController voiceClientController,
        IYouTubeDownloader downloader,
        ILogger<QueuePlaybackService> logger)
    {
        _queueService = queueService;
        _voiceClientController = voiceClientController;
        _downloader = downloader;
        _logger = logger;
    }

    public bool IsProcessing
    {
        get
        {
            lock (_lock)
            {
                return _processingTask != null && !_processingTask.IsCompleted;
            }
        }
    }

    public async Task StartQueueProcessingAsync(Guild guild, GatewayClient client, ulong userId)
    {
        lock (_lock)
        {
            if (IsProcessing)
            {
                _logger.LogDebug("Queue processing is already running");
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _processingTask = ProcessQueueAsync(guild, client, userId, _cancellationTokenSource.Token);
        }

        _logger.LogInformation("Started queue processing for guild {GuildId}", guild.Id);
        await Task.CompletedTask;
    }

    public async Task StopQueueProcessingAsync()
    {
        Task? taskToWait = null;

        lock (_lock)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                taskToWait = _processingTask;
            }
        }

        if (taskToWait != null)
        {
            try
            {
                await taskToWait;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }

        await _voiceClientController.StopPlaybackAsync();
        _queueService.SetCurrentSong(null);
        _logger.LogInformation("Stopped queue processing");
    }

    public async Task SkipCurrentSongAsync()
    {
        await _voiceClientController.StopPlaybackAsync();
        _queueService.SetCurrentSong(null);
        _logger.LogInformation("Skipped current song");
    }

    private async Task ProcessQueueAsync(Guild guild, GatewayClient client, ulong userId, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var nextSong = _queueService.DequeueNext();
                if (nextSong == null)
                {
                    // Wait for songs to be added to the queue
                    // Use exponential backoff to reduce log spam
                    var waitTime = TimeSpan.FromSeconds(5);
                    await Task.Delay(waitTime, cancellationToken);
                    continue;
                }

                _queueService.SetCurrentSong(nextSong);
                _logger.LogInformation("Processing song from queue: {Title}", nextSong.Title);

                try
                {
                    // Download if not already downloaded
                    if (string.IsNullOrWhiteSpace(nextSong.FilePath) || !File.Exists(nextSong.FilePath))
                    {
                        _logger.LogInformation("Downloading audio for: {Title}", nextSong.Title);
                        var filePath = await _downloader.DownloadAsync(nextSong.Url);
                        
                        if (!string.IsNullOrWhiteSpace(filePath))
                        {
                            var normalizedPath = Path.GetFullPath(filePath.Trim());
                            nextSong.FilePath = normalizedPath;
                        }

                        if (string.IsNullOrWhiteSpace(nextSong.FilePath) || !File.Exists(nextSong.FilePath))
                        {
                            _logger.LogWarning("Failed to download audio for: {Title}", nextSong.Title);
                            _queueService.SetCurrentSong(null);
                            continue;
                        }
                    }

                    _logger.LogInformation("Playing song: {Title}", nextSong.Title);
                    var result = await _voiceClientController.PlayMp3Async(guild, client, nextSong.RequestedByUserId, nextSong.FilePath);
                    _logger.LogDebug("Playback result: {Result}", result);

                    // Wait for the song to finish playing
                    await WaitForSongCompletionAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error playing song: {Title}", nextSong.Title);
                }

                _queueService.SetCurrentSong(null);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Queue processing was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in queue processing");
        }
    }

    private async Task WaitForSongCompletionAsync(CancellationToken cancellationToken)
    {
        // This is a simplified approach - in a real implementation you'd want to
        // listen for actual playback completion events from the audio service
        // For now, we'll just wait a reasonable amount of time and check if playback was stopped
        var checkInterval = TimeSpan.FromSeconds(1);
        var maxWaitTime = TimeSpan.FromMinutes(10); // Max song length
        var elapsed = TimeSpan.Zero;

        while (elapsed < maxWaitTime && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(checkInterval, cancellationToken);
            elapsed = elapsed.Add(checkInterval);

            // Check if the current song was cleared (indicating a skip or stop)
            if (_queueService.CurrentSong == null)
            {
                break;
            }
        }
    }
}