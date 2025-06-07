using NetCord.Gateway.Voice;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Orpheus.Services.VoiceClientController;

public class AudioPlaybackService : IAudioPlaybackService
{
    private readonly ILogger<AudioPlaybackService> _logger;
    private Process? _currentFfmpegProcess;
    private readonly object _lock = new();

    public AudioPlaybackService(ILogger<AudioPlaybackService> logger)
    {
        _logger = logger;
    }

    public async Task PlayMp3ToStreamAsync(string filePath, OpusEncodeStream outputStream, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Preparing to start FFMPEG for file: {FilePath}", filePath);

        ProcessStartInfo startInfo = new("ffmpeg")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        var arguments = startInfo.ArgumentList;

        arguments.Add("-i");
        arguments.Add(filePath);
        arguments.Add("-loglevel");
        arguments.Add("error");
        arguments.Add("-ac");
        arguments.Add("2");
        arguments.Add("-f");
        arguments.Add("s16le");
        arguments.Add("-ar");
        arguments.Add("48000");
        arguments.Add("pipe:1");

        _logger.LogInformation("FFMPEG command: ffmpeg {Args}", string.Join(" ", arguments));
        _logger.LogDebug("FFMPEG working directory: {Dir}", startInfo.WorkingDirectory);

        try
        {
            var ffmpeg = new Process { StartInfo = startInfo };
            lock (_lock)
            {
                _currentFfmpegProcess = ffmpeg;
            }
            ffmpeg.Start();
            _logger.LogInformation("FFMPEG process started for file: {FilePath}", filePath);

            var stderrTask = ffmpeg.StandardError.ReadToEndAsync();

            await ffmpeg.StandardOutput.BaseStream.CopyToAsync(outputStream, cancellationToken);
            await outputStream.FlushAsync(cancellationToken);

            _logger.LogInformation("Finished streaming audio for file: {FilePath}", filePath);

            await ffmpeg.WaitForExitAsync(cancellationToken);

            var stderr = await stderrTask;
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                _logger.LogError("FFMPEG stderr for file {FilePath}: {Stderr}", filePath, stderr);
            }

            if (ffmpeg.ExitCode != 0)
            {
                _logger.LogWarning("FFMPEG exited with non-zero code {ExitCode} for file: {FilePath}", ffmpeg.ExitCode, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while streaming audio for file: {FilePath}", filePath);
            throw;
        }
        finally
        {
            lock (_lock)
            {
                _currentFfmpegProcess = null;
            }
        }
    }

    public Task StopPlaybackAsync()
    {
        lock (_lock)
        {
            if (_currentFfmpegProcess != null && !_currentFfmpegProcess.HasExited)
            {
                try
                {
                    _logger.LogInformation("Stopping FFMPEG playback process.");
                    _currentFfmpegProcess.Kill(true);
                    _currentFfmpegProcess = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to stop FFMPEG process.");
                }
            }
        }
        return Task.CompletedTask;
    }
}