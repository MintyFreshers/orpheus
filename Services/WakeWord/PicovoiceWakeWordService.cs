using Concentus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pv;
using System.Collections.Concurrent;

namespace Orpheus.Services.WakeWord;

public class PicovoiceWakeWordService : IWakeWordDetectionService, IDisposable
{
    private readonly ILogger<PicovoiceWakeWordService> _logger;
    private readonly IConfiguration _configuration;
    private Porcupine? _porcupine;
    private bool _isInitialized;
    private readonly object _initLock = new();
    private readonly ConcurrentDictionary<ulong, long> _lastDetectionTimes = new();
    private readonly Dictionary<ulong, List<short>> _pcmBuffers = new(); // Buffer for each user
    // Cooldown period in milliseconds to prevent multiple detections too close together
    private const int DETECTION_COOLDOWN_MS = 3000;
    private readonly IOpusDecoder _opusDecoder;
    private const int DISCORD_SAMPLE_RATE = 48000;
    private const int PICOVOICE_SAMPLE_RATE = 16000;
    private const int FRAME_LENGTH_MS = 20;
    private const int DISCORD_FRAME_SIZE = DISCORD_SAMPLE_RATE / 1000 * FRAME_LENGTH_MS;
    private const int PICOVOICE_FRAME_SIZE = PICOVOICE_SAMPLE_RATE / 1000 * FRAME_LENGTH_MS;

    public bool IsInitialized => _isInitialized;

    public event Action<ulong>? WakeWordDetected;

    public PicovoiceWakeWordService(ILogger<PicovoiceWakeWordService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _opusDecoder = OpusCodecFactory.CreateDecoder(DISCORD_SAMPLE_RATE, 1); // 48kHz sample rate, 1 channel (mono)
    }

    public void Initialize()
    {
        lock (_initLock)
        {
            if (_isInitialized)
                return;

            try
            {
                // Check for the wake word model file
                string keywordPath = "Resources/orpheus_keyword_file.ppn";
                if (!File.Exists(keywordPath))
                {
                    _logger.LogError("Wake word model file not found at: {Path}", keywordPath);
                    return;
                }

                var picovoiceAccessKey = _configuration["PicovoiceAccessKey"];
                if (string.IsNullOrEmpty(picovoiceAccessKey))
                {
                    _logger.LogError("Picovoice access key not found in configuration.");
                    return;
                }

                // Initialize Porcupine with the keyword "orpheus"
                _porcupine = Porcupine.FromKeywordPaths(
                    accessKey: picovoiceAccessKey,
                    keywordPaths: new[] { keywordPath },
                    sensitivities: new[] { 0.5f }
                );

                _isInitialized = true;
                _logger.LogInformation("Wake word detection initialized with keyword 'orpheus'");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize wake word detection");
                Cleanup();
            }
        }
    }

    public bool ProcessAudioFrame(byte[] opusFrame, ulong userId)
    {
        if (!_isInitialized || _porcupine == null)
            return false;

        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_lastDetectionTimes.TryGetValue(userId, out var lastDetection) &&
                now - lastDetection < DETECTION_COOLDOWN_MS)
            {
                return false;
            }

            short[] pcmSamples = ConvertOpusFrameToPcm(opusFrame);
            int requiredFrameLength = _porcupine.FrameLength;

            // Buffer PCM samples for this user
            if (!_pcmBuffers.TryGetValue(userId, out var buffer))
            {
                buffer = new List<short>();
                _pcmBuffers[userId] = buffer;
            }
            buffer.AddRange(pcmSamples);

            bool detected = false;
            // Process as many full frames as possible
            while (buffer.Count >= requiredFrameLength)
            {
                short[] frame = buffer.GetRange(0, requiredFrameLength).ToArray();
                buffer.RemoveRange(0, requiredFrameLength);
                int keywordIndex = _porcupine.Process(frame);
                if (keywordIndex != -1)
                {
                    _logger.LogInformation($"Wake word 'orpheus' detected from user {userId}!");
                    _lastDetectionTimes[userId] = now;
                    detected = true;
                    WakeWordDetected?.Invoke(userId);
                    break; // Only report one detection per call
                }
            }
            return detected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio frame for wake word detection");
            return false;
        }
    }

    private short[] ConvertOpusFrameToPcm(byte[] opusFrame)
    {
        // Decode the Opus frame to raw PCM
        int frameSize = DISCORD_FRAME_SIZE;
        short[] pcm = new short[frameSize];
        _opusDecoder.Decode(opusFrame, pcm, frameSize);

        // Resample from Discord's 48kHz to Porcupine's required 16kHz
        short[] resampledPcm = new short[PICOVOICE_FRAME_SIZE];
        int resampleFactor = DISCORD_SAMPLE_RATE / PICOVOICE_SAMPLE_RATE;
        for (int i = 0; i < PICOVOICE_FRAME_SIZE; i++)
        {
            resampledPcm[i] = pcm[i * resampleFactor];
        }

        return resampledPcm;
    }

    public void Cleanup()
    {
        lock (_initLock)
        {
            if (_porcupine != null)
            {
                _porcupine.Dispose();
                _porcupine = null;
            }
            _isInitialized = false;
        }
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}