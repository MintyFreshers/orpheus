using Microsoft.Extensions.Logging;

namespace Orpheus.Services.Queue;

public class SongQueueService : ISongQueueService
{
    private readonly LinkedList<QueuedSong> _queue = new();
    private readonly object _lock = new();
    private readonly ILogger<SongQueueService> _logger;
    private QueuedSong? _currentSong;

    public SongQueueService(ILogger<SongQueueService> logger)
    {
        _logger = logger;
    }

    public QueuedSong? CurrentSong
    {
        get
        {
            lock (_lock)
            {
                return _currentSong;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count == 0;
            }
        }
    }

    public void EnqueueSong(QueuedSong song)
    {
        lock (_lock)
        {
            _queue.AddLast(song);
            _logger.LogInformation("Song queued: {Title} (requested by {UserId}). Queue size: {QueueSize}", 
                song.Title, song.RequestedByUserId, _queue.Count);
        }
    }

    public void EnqueueSongNext(QueuedSong song)
    {
        lock (_lock)
        {
            _queue.AddFirst(song);
            _logger.LogInformation("Song queued next: {Title} (requested by {UserId}). Queue size: {QueueSize}", 
                song.Title, song.RequestedByUserId, _queue.Count);
        }
    }

    public QueuedSong? PeekNext()
    {
        lock (_lock)
        {
            return _queue.Count > 0 ? _queue.First!.Value : null;
        }
    }

    public QueuedSong? DequeueNext()
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                return null;
            }

            var song = _queue.First!.Value;
            _queue.RemoveFirst();
            _logger.LogInformation("Song dequeued: {Title}. Remaining queue size: {QueueSize}", 
                song.Title, _queue.Count);
            return song;
        }
    }

    public IReadOnlyList<QueuedSong> GetQueue()
    {
        lock (_lock)
        {
            return _queue.ToList().AsReadOnly();
        }
    }

    public void ClearQueue()
    {
        lock (_lock)
        {
            var count = _queue.Count;
            _queue.Clear();
            _logger.LogInformation("Queue cleared. Removed {Count} songs", count);
        }
    }

    public void SetCurrentSong(QueuedSong? song)
    {
        lock (_lock)
        {
            _currentSong = song;
            if (song != null)
            {
                _logger.LogInformation("Current song set to: {Title}", song.Title);
            }
            else
            {
                _logger.LogInformation("Current song cleared");
            }
        }
    }
}