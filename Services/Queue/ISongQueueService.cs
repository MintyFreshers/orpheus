namespace Orpheus.Services.Queue;

public interface ISongQueueService
{
    /// <summary>
    /// Adds a song to the queue
    /// </summary>
    void EnqueueSong(QueuedSong song);

    /// <summary>
    /// Adds a song to the front of the queue (next to play)
    /// </summary>
    void EnqueueSongNext(QueuedSong song);

    /// <summary>
    /// Gets the next song from the queue without removing it
    /// </summary>
    QueuedSong? PeekNext();

    /// <summary>
    /// Removes and returns the next song from the queue
    /// </summary>
    QueuedSong? DequeueNext();

    /// <summary>
    /// Gets all songs currently in the queue
    /// </summary>
    IReadOnlyList<QueuedSong> GetQueue();

    /// <summary>
    /// Clears all songs from the queue
    /// </summary>
    void ClearQueue();

    /// <summary>
    /// Gets the currently playing song, if any
    /// </summary>
    QueuedSong? CurrentSong { get; }

    /// <summary>
    /// Sets the currently playing song
    /// </summary>
    void SetCurrentSong(QueuedSong? song);

    /// <summary>
    /// Gets the total number of songs in the queue
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Checks if the queue is empty
    /// </summary>
    bool IsEmpty { get; }
}