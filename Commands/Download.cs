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
        _ = _downloader.DownloadAsync(url);
        return Task.FromResult("Download requested.");
    }
}