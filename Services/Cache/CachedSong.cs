namespace Orpheus.Services.Cache;

public class CachedSong
{
    public string UniqueId { get; }
    public string Title { get; set; }
    public string Url { get; }
    public string FilePath { get; }
    public DateTimeOffset CachedAt { get; }
    public DateTimeOffset LastAccessedAt { get; set; }
    public long FileSizeBytes { get; }

    public CachedSong(string uniqueId, string title, string url, string filePath, long fileSizeBytes)
    {
        UniqueId = uniqueId;
        Title = title;
        Url = url;
        FilePath = filePath;
        FileSizeBytes = fileSizeBytes;
        CachedAt = DateTimeOffset.UtcNow;
        LastAccessedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateLastAccessed()
    {
        LastAccessedAt = DateTimeOffset.UtcNow;
    }
}