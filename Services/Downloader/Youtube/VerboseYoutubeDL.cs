using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace Orpheus.Services.Downloader.Youtube;

public class VerboseYoutubeDL : YoutubeDL
{
    public Action<string> Log { get; set; }

    public VerboseYoutubeDL(Action<string> log = null)
    {
        Log = log;
    }

    public async Task<RunResult<string>> RunAudioDownloadVerbose(
        string url,
        AudioConversionFormat format = AudioConversionFormat.Best,
        CancellationToken ct = default)
    {
        Log?.Invoke($"[YoutubeDL] Starting audio download for URL: {url}");

        var result = await this.RunAudioDownload(url, format, ct);

        Log?.Invoke($"[YoutubeDL] yt-dlp exited with code {(result.Success ? 0 : 1)}");
        if (result.ErrorOutput != null && result.ErrorOutput.Length > 0)
        {
            foreach (var err in result.ErrorOutput)
                Log?.Invoke($"[YoutubeDL] yt-dlp error: {err}");
        }
        if (!string.IsNullOrWhiteSpace(result.Data))
        {
            Log?.Invoke($"[YoutubeDL] Downloaded file: {result.Data}");
        }
        return result;
    }
}