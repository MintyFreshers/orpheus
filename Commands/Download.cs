using NetCord.Services.ApplicationCommands;
using Orpheus.Services.Downloader.Youtube;

namespace Orpheus.Commands;

public class Download : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IYouTubeDownloader _downloader;

    public Download(IYouTubeDownloader downloader)
    {
        _downloader = downloader;
    }

    [SlashCommand("download", "Download a YouTube video by URL")]
    public Task<string> DownloadCommandAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Task.FromResult("Invalid URL. Please provide a valid YouTube video URL.");
        }

        _ = _downloader.DownloadAsync(url);
        return Task.FromResult("Download requested.");
    }
}