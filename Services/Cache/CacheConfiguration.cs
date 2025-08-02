namespace Orpheus.Services.Cache;

public class CacheConfiguration
{
    /// <summary>
    /// Maximum number of cached files (0 = unlimited)
    /// </summary>
    public int MaxFiles { get; set; } = 100;

    /// <summary>
    /// Maximum cache size in bytes (0 = unlimited)
    /// </summary>
    public long MaxSizeBytes { get; set; } = 1024L * 1024L * 1024L; // 1GB default

    /// <summary>
    /// Directory where cached files are stored
    /// </summary>
    public string CacheDirectory { get; set; } = "Downloads";

    /// <summary>
    /// File name for cache metadata storage
    /// </summary>
    public string MetadataFileName { get; set; } = "cache_metadata.json";

    /// <summary>
    /// Whether to enable automatic cache cleanup
    /// </summary>
    public bool EnableAutomaticCleanup { get; set; } = true;

    /// <summary>
    /// How often to run automatic cleanup (in minutes)
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 60;
}