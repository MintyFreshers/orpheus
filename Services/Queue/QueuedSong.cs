namespace Orpheus.Services.Queue;

public class QueuedSong
{
    public string Id { get; }
    public string Title { get; }
    public string Url { get; }
    public string FilePath { get; }
    public DateTimeOffset QueuedAt { get; }
    public ulong RequestedByUserId { get; }

    public QueuedSong(string title, string url, string filePath, ulong requestedByUserId)
    {
        Id = Guid.NewGuid().ToString();
        Title = title;
        Url = url;
        FilePath = filePath;
        QueuedAt = DateTimeOffset.UtcNow;
        RequestedByUserId = requestedByUserId;
    }
}