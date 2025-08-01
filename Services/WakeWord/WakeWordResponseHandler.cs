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
    private const int DiscordSampleRate = 48000;
    private const int TranscriptionTimeoutMs = 5000;
    private const int FrameLengthMs = 20;
    private const int DiscordFrameSize = DiscordSampleRate / 1000 * FrameLengthMs;

    private readonly ILogger<WakeWordResponseHandler> _logger;
    private readonly BotConfiguration _discordConfiguration;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IVoiceCommandProcessor _voiceCommandProcessor;
    private readonly ConcurrentDictionary<ulong, UserTranscriptionSession> _activeSessions = new();
    private readonly IOpusDecoder _opusDecoder;

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
        _opusDecoder = OpusCodecFactory.CreateDecoder(DiscordSampleRate, 1);
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
            _logger.LogInformation("Wake word detected from user {UserId}, starting transcription session", userId);

            await InitiateTranscriptionSessionAsync(userId, client);
            await SendListeningResponseAsync(userId, client);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send wake word response message");
        }
    }

    public Task ProcessAudioForTranscription(byte[] opusFrame, ulong userId)
    {
        if (!_activeSessions.TryGetValue(userId, out var session))
        {
            return Task.CompletedTask;
        }

        try
        {
            var pcmAudioData = ConvertOpusFrameToPcmBytes(opusFrame);
            session.AudioData.AddRange(pcmAudioData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio frame for transcription");
        }

        return Task.CompletedTask;
    }

    private async Task InitiateTranscriptionSessionAsync(ulong userId, GatewayClient client)
    {
        var session = CreateNewTranscriptionSession(userId, client);
        _activeSessions[userId] = session;

        await ScheduleSessionTimeoutAsync(userId);
        _logger.LogInformation("Started transcription session for user {UserId}", userId);
    }

    private static UserTranscriptionSession CreateNewTranscriptionSession(ulong userId, GatewayClient client)
    {
        return new UserTranscriptionSession
        {
            UserId = userId,
            StartTime = DateTime.UtcNow,
            AudioData = new List<byte>(),
            Client = client
        };
    }

    private Task ScheduleSessionTimeoutAsync(ulong userId)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TranscriptionTimeoutMs);
            await CompleteTranscriptionSessionAsync(userId);
        });
        
        return Task.CompletedTask;
    }

    private async Task SendListeningResponseAsync(ulong userId, GatewayClient client)
    {
        var channelId = _discordConfiguration.DefaultChannelId;
        var listeningMessage = CreateListeningMessage(userId);
        await client.Rest.SendMessageAsync(channelId, listeningMessage);
    }

    private async Task CompleteTranscriptionSessionAsync(ulong userId)
    {
        if (!_activeSessions.TryRemove(userId, out var session))
        {
            return;
        }

        try
        {
            _logger.LogInformation("Ending transcription session for user {UserId}", userId);

            if (session.AudioData.Count > 0)
            {
                await ProcessCollectedAudioAsync(session);
            }
            else
            {
                await SendNoAudioResponseAsync(userId, session.Client);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending transcription session for user {UserId}", userId);
        }
    }

    private async Task ProcessCollectedAudioAsync(UserTranscriptionSession session)
    {
        var audioBytes = session.AudioData.ToArray();
        var transcription = await _transcriptionService.TranscribeAudioAsync(audioBytes);

        if (!string.IsNullOrEmpty(transcription))
        {
            await ProcessSuccessfulTranscriptionAsync(session, transcription);
        }
        else
        {
            await SendNoTranscriptionResponseAsync(session);
        }
    }

    private async Task ProcessSuccessfulTranscriptionAsync(UserTranscriptionSession session, string transcription)
    {
        var response = await _voiceCommandProcessor.ProcessCommandAsync(transcription, session.UserId);
        var channelId = _discordConfiguration.DefaultChannelId;
        await session.Client.Rest.SendMessageAsync(channelId, new MessageProperties().WithContent(response));
    }

    private async Task SendNoTranscriptionResponseAsync(UserTranscriptionSession session)
    {
        _logger.LogWarning("No transcription result for user {UserId}", session.UserId);
        var channelId = _discordConfiguration.DefaultChannelId;
        var noTranscriptionMessage = CreateNoTranscriptionMessage(session.UserId);
        await session.Client.Rest.SendMessageAsync(channelId, noTranscriptionMessage);
    }

    private async Task SendNoAudioResponseAsync(ulong userId, GatewayClient client)
    {
        _logger.LogWarning("No audio data collected for user {UserId}", userId);
        var channelId = _discordConfiguration.DefaultChannelId;
        var noAudioMessage = CreateNoTranscriptionMessage(userId);
        await client.Rest.SendMessageAsync(channelId, noAudioMessage);
    }

    private byte[] ConvertOpusFrameToPcmBytes(byte[] opusFrame)
    {
        var pcmSamples = DecodeOpusFrameToPcmSamples(opusFrame);
        return ConvertPcmSamplesToBytes(pcmSamples);
    }

    private short[] DecodeOpusFrameToPcmSamples(byte[] opusFrame)
    {
        int frameSize = DiscordFrameSize;
        short[] pcm = new short[frameSize];
        _opusDecoder.Decode(opusFrame, pcm, frameSize);
        return pcm;
    }

    private static byte[] ConvertPcmSamplesToBytes(short[] pcmSamples)
    {
        byte[] pcmBytes = new byte[pcmSamples.Length * 2];
        Buffer.BlockCopy(pcmSamples, 0, pcmBytes, 0, pcmBytes.Length);
        return pcmBytes;
    }

    private static MessageProperties CreateListeningMessage(ulong userId)
    {
        return new MessageProperties().WithContent($"<@{userId}> I'm listening...");
    }

    private static MessageProperties CreateNoTranscriptionMessage(ulong userId)
    {
        return new MessageProperties().WithContent($"<@{userId}> I didn't hear anything clearly.");
    }
}

internal class UserTranscriptionSession
{
    public ulong UserId { get; set; }
    public DateTime StartTime { get; set; }
    public List<byte> AudioData { get; set; } = new();
    public GatewayClient Client { get; set; } = null!;
}
