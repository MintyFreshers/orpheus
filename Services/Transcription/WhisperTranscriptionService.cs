using Microsoft.Extensions.Logging;
using Whisper.net;
using Whisper.net.Ggml;
using System.Collections.Concurrent;
using NAudio.Wave;
using NAudio.Dsp;

namespace Orpheus.Services.Transcription;

public class WhisperTranscriptionService : ITranscriptionService, IDisposable
{
    private readonly ILogger<WhisperTranscriptionService> _logger;
    private WhisperFactory? _whisperFactory;
    private WhisperProcessor? _whisperProcessor;
    private bool _isInitialized;
    private readonly object _initLock = new();
    private const string MODEL_NAME = "ggml-base.bin";
    private const bool ENABLE_DEBUG_AUDIO_SAVING = true; // Set to false in production

    public bool IsInitialized => _isInitialized;

    public WhisperTranscriptionService(ILogger<WhisperTranscriptionService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        try
        {
            _logger.LogInformation("Initializing Whisper transcription service...");
            
            // Use built-in model downloading from Whisper.net
            var modelPath = Path.Combine(Environment.CurrentDirectory, "Models", "ggml-tiny.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);

            if (!File.Exists(modelPath))
            {
                _logger.LogInformation("Downloading Whisper tiny model...");
                
                // Create an instance of the downloader with HttpClient
                using var httpClient = new HttpClient();
                var downloader = new WhisperGgmlDownloader(httpClient);
                using var modelStream = await downloader.GetGgmlModelAsync(GgmlType.Tiny);
                using var fileWriter = File.Create(modelPath);
                await modelStream.CopyToAsync(fileWriter);
                _logger.LogInformation("Model downloaded successfully");
            }

            lock (_initLock)
            {
                _whisperFactory = WhisperFactory.FromPath(modelPath);
                _whisperProcessor = _whisperFactory.CreateBuilder()
                    .WithLanguage("en")
                    .Build();

                _isInitialized = true;
            }
            
            _logger.LogInformation("Whisper transcription service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Whisper transcription service");
            Cleanup();
        }
    }

    public async Task<string?> TranscribeAudioAsync(byte[] audioData)
    {
        if (!_isInitialized || _whisperProcessor == null)
        {
            _logger.LogWarning("Transcription service not initialized");
            return null;
        }

        try
        {
            // Save debug audio if enabled
            if (ENABLE_DEBUG_AUDIO_SAVING)
            {
                SaveDebugAudioAsync(audioData, "raw_input");
            }

            // Convert audio data to the format expected by Whisper
            // Whisper expects float array with sample rate 16kHz
            var samples = ConvertTo16kHzFloatArray(audioData);
            
            // Save resampled audio for debugging
            if (ENABLE_DEBUG_AUDIO_SAVING)
            {
                SaveDebugResampledAudioAsync(samples, "resampled_16khz");
            }
            
            await foreach (var segment in _whisperProcessor.ProcessAsync(samples))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text))
                {
                    var transcription = segment.Text.Trim();
                    _logger.LogInformation("Transcribed: {Text}", transcription);
                    return transcription;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during transcription");
            return null;
        }
    }

    private float[] ConvertTo16kHzFloatArray(byte[] audioData)
    {
        // Discord audio is 48kHz, 16-bit PCM, mono
        // Whisper expects 16kHz, 32-bit float, mono
        
        const int sourceRate = 48000;
        const int targetRate = 16000;
        
        // Convert byte array to 16-bit samples first
        var sourceSamples = new short[audioData.Length / 2];
        for (int i = 0; i < sourceSamples.Length; i++)
        {
            sourceSamples[i] = BitConverter.ToInt16(audioData, i * 2);
        }
        
        // Convert to float samples (normalize to [-1, 1])
        var sourceFloats = new float[sourceSamples.Length];
        for (int i = 0; i < sourceSamples.Length; i++)
        {
            sourceFloats[i] = sourceSamples[i] / 32768.0f;
        }
        
        // Resample from 48kHz to 16kHz using simple decimation
        // More sophisticated resampling could be used but this should work for speech
        int decimationFactor = sourceRate / targetRate; // 48000 / 16000 = 3
        var resampledSamples = new float[sourceFloats.Length / decimationFactor];
        
        for (int i = 0; i < resampledSamples.Length; i++)
        {
            resampledSamples[i] = sourceFloats[i * decimationFactor];
        }
        
        _logger.LogDebug("Resampled audio from {SourceSamples} samples at {SourceRate}Hz to {TargetSamples} samples at {TargetRate}Hz", 
            sourceFloats.Length, sourceRate, resampledSamples.Length, targetRate);
        
        return resampledSamples;
    }

    private void SaveDebugAudioAsync(byte[] audioData, string prefix)
    {
        try
        {
            var debugDir = Path.Combine(Environment.CurrentDirectory, "DebugAudio");
            Directory.CreateDirectory(debugDir);
            
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var filename = $"{prefix}_{timestamp}.wav";
            var filePath = Path.Combine(debugDir, filename);
            
            // Save as 48kHz 16-bit PCM WAV file
            using var writer = new WaveFileWriter(filePath, new WaveFormat(48000, 16, 1));
            writer.Write(audioData, 0, audioData.Length);
            
            _logger.LogDebug("Saved debug audio: {FilePath} ({Size} bytes)", filePath, audioData.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save debug audio with prefix {Prefix}", prefix);
        }
    }

    private void SaveDebugResampledAudioAsync(float[] samples, string prefix)
    {
        try
        {
            var debugDir = Path.Combine(Environment.CurrentDirectory, "DebugAudio");
            Directory.CreateDirectory(debugDir);
            
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var filename = $"{prefix}_{timestamp}.wav";
            var filePath = Path.Combine(debugDir, filename);
            
            // Convert float samples back to 16-bit for WAV file
            var int16Samples = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                int16Samples[i] = (short)(Math.Max(-1.0f, Math.Min(1.0f, samples[i])) * 32767);
            }
            
            // Save as 16kHz 16-bit PCM WAV file
            using var writer = new WaveFileWriter(filePath, new WaveFormat(16000, 16, 1));
            byte[] bytes = new byte[int16Samples.Length * 2];
            Buffer.BlockCopy(int16Samples, 0, bytes, 0, bytes.Length);
            writer.Write(bytes, 0, bytes.Length);
            
            _logger.LogDebug("Saved debug resampled audio: {FilePath} ({Samples} samples)", filePath, samples.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save debug resampled audio with prefix {Prefix}", prefix);
        }
    }

    public void Cleanup()
    {
        lock (_initLock)
        {
            _whisperProcessor?.Dispose();
            _whisperProcessor = null;
            _whisperFactory?.Dispose();
            _whisperFactory = null;
            _isInitialized = false;
            _logger.LogInformation("Whisper transcription service cleaned up");
        }
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}