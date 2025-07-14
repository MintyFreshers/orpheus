using Microsoft.Extensions.Logging;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace Orpheus.Services.Downloader.Youtube;

public class VerboseYoutubeDL : YoutubeDL
{
    private readonly ILogger<VerboseYoutubeDL> _logger;

    public VerboseYoutubeDL(ILogger<VerboseYoutubeDL> logger)
    {
        _logger = logger;
    }

    public async Task<RunResult<string>> RunAudioDownloadVerbose(
        string url,
        AudioConversionFormat format = AudioConversionFormat.Best,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[YoutubeDL] Starting audio download for URL: {Url}", url);

        try
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var cpuTime = proc.TotalProcessorTime;
            var mem = proc.WorkingSet64 / (1024 * 1024);
            _logger.LogDebug("[Perf] Before yt-dlp: CPU Time={CpuTime}ms, Memory={MemoryMB}MB", cpuTime.TotalMilliseconds, mem);
        }
        catch { }

        var result = await RunAudioDownload(url, format, ct);

        try
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var cpuTime = proc.TotalProcessorTime;
            var mem = proc.WorkingSet64 / (1024 * 1024);
            _logger.LogDebug("[Perf] After yt-dlp: CPU Time={CpuTime}ms, Memory={MemoryMB}MB", cpuTime.TotalMilliseconds, mem);
        }
        catch { }

        _logger.LogInformation("[YoutubeDL] yt-dlp exited with code {Code}", result.Success ? 0 : 1);
        if (result.ErrorOutput != null && result.ErrorOutput.Length > 0)
        {
            foreach (var err in result.ErrorOutput)
            {
                _logger.LogError("[YoutubeDL] yt-dlp error: {Error}", err);
            }
        }
        if (!string.IsNullOrWhiteSpace(result.Data))
        {
            _logger.LogInformation("[YoutubeDL] Downloaded file: {File}", result.Data);
        }
        return result;
    }
}