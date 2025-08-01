using Microsoft.Extensions.Logging;
using Whisper.net;
using Whisper.net.Ggml;
using System.Collections.Concurrent;

namespace Orpheus.Services.Transcription;

public class WhisperTranscriptionService : ITranscriptionService, IDisposable
{
    private readonly ILogger<WhisperTranscriptionService> _logger;
    private WhisperFactory? _whisperFactory;
    private WhisperProcessor? _whisperProcessor;
    private bool _isInitialized;
    private readonly object _initLock = new();
    private const string MODEL_NAME = "ggml-base.bin";

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
            
            // For simplicity, try to use a lightweight model or download it differently
            // We'll use the tiny model for faster processing
            var modelPath = Path.Combine(Environment.CurrentDirectory, "Models", "ggml-tiny.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);

            if (!File.Exists(modelPath))
            {
                _logger.LogInformation("Downloading Whisper tiny model for faster processing...");
                
                // Use the correct static method call
                using var httpClient = new HttpClient();
                var modelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin";
                var response = await httpClient.GetStreamAsync(modelUrl);
                using var fileWriter = File.OpenWrite(modelPath);
                await response.CopyToAsync(fileWriter);
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
            // Convert audio data to the format expected by Whisper
            // Whisper expects float array with sample rate 16kHz
            var samples = ConvertToFloatArray(audioData);
            
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

    private float[] ConvertToFloatArray(byte[] audioData)
    {
        // Convert byte array to float array
        // This assumes the audio data is in 16-bit PCM format
        var samples = new float[audioData.Length / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = BitConverter.ToInt16(audioData, i * 2);
            samples[i] = sample / 32768.0f; // Convert to float range [-1, 1]
        }
        return samples;
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