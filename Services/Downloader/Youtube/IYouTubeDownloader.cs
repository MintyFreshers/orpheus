namespace Orpheus.Services;

public interface IYouTubeDownloader
{
    Task<string> DownloadAsync(string url);
}