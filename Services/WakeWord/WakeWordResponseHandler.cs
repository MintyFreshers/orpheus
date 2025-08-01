using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using Orpheus.Configuration;
using Orpheus.Services.Transcription;
using System.Collections.Concurrent;
using Concentus;

namespace Orpheus.Services.WakeWord;

public class WakeWordResponseHandler
{
    private readonly ILogger<WakeWordResponseHandler> _logger;
    private readonly BotConfiguration _discordConfiguration;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IVoiceCommandProcessor _voiceCommandProcessor;
    private readonly ConcurrentDictionary<ulong, UserTranscriptionSession> _transcriptionSessions = new();
    private readonly IOpusDecoder _opusDecoder;
    
    private const int DISCORD_SAMPLE_RATE = 48000;
    private const int TRANSCRIPTION_TIMEOUT_MS = 5000; // 5 seconds to speak after wake word
    private const int FRAME_LENGTH_MS = 20;
    private const int DISCORD_FRAME_SIZE = DISCORD_SAMPLE_RATE / 1000 * FRAME_LENGTH_MS;

    public WakeWordResponseHandler(
        ILogger<WakeWordResponseHandler> logger, 
        BotConfiguration discordConfiguration,
        ITranscriptionService transcriptionService,
        IVoiceCommandProcessor voiceCommandProcessor)
    {
        _logger = logger;
        _discordConfiguration = discordConfiguration;
        _transcriptionService = transcriptionService;
        _voiceCommandProcessor = voiceCommandProcessor;
        _opusDecoder = OpusCodecFactory.CreateDecoder(DISCORD_SAMPLE_RATE, 1);
    }

    public async Task HandleWakeWordDetectionAsync(ulong userId, GatewayClient? client)
    {
        if (client == null)
        {
            _logger.LogWarning("Cannot send wake word response: Gateway client is null");
            return;
        }

        try
        {
            var channelId = _discordConfiguration.DefaultChannelId;
            
            _logger.LogInformation("Wake word detected from user {UserId}, starting transcription session", userId);
            
            // Start transcription session for this user
            StartTranscriptionSession(userId, client);
            
            // Send initial response
            var mentionMessage = CreateWakeWordResponseMessage(userId);
            await client.Rest.SendMessageAsync(channelId, mentionMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send wake word response message");
        }
    }

    private void StartTranscriptionSession(ulong userId, GatewayClient client)
    {
        var session = new UserTranscriptionSession
        {
            UserId = userId,
            StartTime = DateTime.UtcNow,
            AudioData = new List<byte>(),
            Client = client
        };

        _transcriptionSessions[userId] = session;

        // Set timeout for transcription session
        _ = Task.Run(async () =>
        {
            await Task.Delay(TRANSCRIPTION_TIMEOUT_MS);
            await EndTranscriptionSession(userId);
        });

        _logger.LogInformation("Started transcription session for user {UserId}", userId);
    }

    public Task ProcessAudioForTranscription(byte[] opusFrame, ulong userId)
    {
        if (!_transcriptionSessions.TryGetValue(userId, out var session))
        {
            return Task.CompletedTask; // No active transcription session for this user
        }

        try
        {
            // Convert Opus frame to PCM
            short[] pcmSamples = ConvertOpusFrameToPcm(opusFrame);
            
            // Convert to bytes and add to session buffer
            byte[] pcmBytes = new byte[pcmSamples.Length * 2];
            Buffer.BlockCopy(pcmSamples, 0, pcmBytes, 0, pcmBytes.Length);
            
            session.AudioData.AddRange(pcmBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio frame for transcription");
        }
        
        return Task.CompletedTask;
    }

    private async Task EndTranscriptionSession(ulong userId)
    {
        if (!_transcriptionSessions.TryRemove(userId, out var session))
        {
            return;
        }

        try
        {
            _logger.LogInformation("Ending transcription session for user {UserId}", userId);

            if (session.AudioData.Count > 0)
            {
                // Transcribe the collected audio
                var audioBytes = session.AudioData.ToArray();
                var transcription = await _transcriptionService.TranscribeAudioAsync(audioBytes);

                if (!string.IsNullOrEmpty(transcription))
                {
                    // Process the voice command
                    var response = await _voiceCommandProcessor.ProcessCommandAsync(transcription, userId);
                    
                    // Send response to Discord
                    var channelId = _discordConfiguration.DefaultChannelId;
                    await session.Client.Rest.SendMessageAsync(channelId, new MessageProperties().WithContent(response));
                }
                else
                {
                    _logger.LogWarning("No transcription result for user {UserId}", userId);
                    var channelId = _discordConfiguration.DefaultChannelId;
                    await session.Client.Rest.SendMessageAsync(channelId, 
                        new MessageProperties().WithContent($"<@{userId}> I didn't hear anything clearly."));
                }
            }
            else
            {
                _logger.LogWarning("No audio data collected for user {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending transcription session for user {UserId}", userId);
        }
    }

    private short[] ConvertOpusFrameToPcm(byte[] opusFrame)
    {
        int frameSize = DISCORD_FRAME_SIZE;
        short[] pcm = new short[frameSize];
        _opusDecoder.Decode(opusFrame, pcm, frameSize);
        return pcm;
    }

    private MessageProperties CreateWakeWordResponseMessage(ulong userId)
    {
        return new MessageProperties()
            .WithContent($"<@{userId}> I'm listening...");
    }
}

internal class UserTranscriptionSession
{
    public ulong UserId { get; set; }
    public DateTime StartTime { get; set; }
    public List<byte> AudioData { get; set; } = new();
    public GatewayClient Client { get; set; } = null!;
}
