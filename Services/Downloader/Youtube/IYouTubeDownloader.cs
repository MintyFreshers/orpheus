namespace Orpheus.Services.Downloader.Youtube;

public interface IYouTubeDownloader
{
    Task<string?> DownloadAsync(string url);
    Task<string?> GetVideoTitleAsync(string url);
    Task<string?> SearchAndGetFirstUrlAsync(string searchQuery);
}