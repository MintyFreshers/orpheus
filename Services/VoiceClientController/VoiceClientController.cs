using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;
using Microsoft.Extensions.Logging;
using NetCord.Rest;

namespace Orpheus.Services.VoiceClientController;

public class VoiceClientController : IVoiceClientController
{
    private readonly ILogger<VoiceClientController> _logger;
    private readonly IAudioPlaybackService _audioPlaybackService;
    private readonly string _instanceGuid;
    private VoiceClient? _voiceClient;

    public VoiceClientController(ILogger<VoiceClientController> logger, IAudioPlaybackService audioPlaybackService)
    {
        _logger = logger;
        _audioPlaybackService = audioPlaybackService;
        _instanceGuid = Guid.NewGuid().ToString();
        _logger.LogInformation("VoiceChannelController instance created with GUID: {Guid}", _instanceGuid);
    }

    public async Task<string> JoinVoiceChannelOfUserAsync(Guild guild, GatewayClient client, ulong userId)
    {
        if (IsBotInVoiceChannel(guild, client.Id))
        {
            return "I'm already connected to a voice channel!";
        }

        var userVoiceState = GetUserVoiceState(guild, userId);
        if (userVoiceState == null)
        {
            return "You are not connected to any voice channel!";
        }

        _voiceClient = await client.JoinVoiceChannelAsync(
            guild.Id,
            userVoiceState.ChannelId.GetValueOrDefault(),
            new VoiceClientConfiguration
            {
                RedirectInputStreams = true,
                Logger = new ConsoleLogger(),
            });

        await _voiceClient.StartAsync();
        await _voiceClient.EnterSpeakingStateAsync(SpeakingFlags.Microphone);

        return "Joined voice channel.";
    }

    public async Task<string> LeaveVoiceChannelAsync(Guild guild, GatewayClient client)
    {
        if (!IsBotInVoiceChannel(guild, client.Id))
        {
            return "I'm not connected to any voice channel!";
        }

        await DisconnectBotFromVoiceChannel(guild, client);

        if (_voiceClient != null)
        {
            _voiceClient.Dispose();
            _voiceClient = null;
            return "Left the voice channel.";
        }
        return "Voice client is not initialized, try manually disconnecting Orpheus.";
    }

    public async Task<string> StartEchoingAsync(Guild guild, GatewayClient client, ulong userId)
    {
        if (!IsBotInVoiceChannel(guild, client.Id) || _voiceClient == null)
        {
            var joinResult = await JoinVoiceChannelOfUserAsync(guild, client, userId);
            if (_voiceClient == null)
            {
                return joinResult;
            }
        }

        var outputStream = _voiceClient!.CreateOutputStream(normalizeSpeed: false);
        _voiceClient.VoiceReceive += args =>
        {
            if (args.UserId == userId)
            {
                return outputStream.WriteAsync(args.Frame);
            }
            return default;
        };

        return "Echoing your voice!";
    }

    public async Task<string> PlayMp3Async(Guild guild, GatewayClient client, ulong userId, string filePath)
    {
        if (!IsBotInVoiceChannel(guild, client.Id) || _voiceClient == null)
        {
            var joinResult = await JoinVoiceChannelOfUserAsync(guild, client, userId);
            if (_voiceClient == null)
            {
                _logger.LogWarning("Voice client was not initialized after join attempt.");
                return joinResult;
            }
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Requested MP3 file not found: {FilePath}", filePath);
            return $"File not found: {filePath}";
        }

        try
        {
            await _voiceClient!.EnterSpeakingStateAsync(SpeakingFlags.Microphone);

            var outStream = _voiceClient.CreateOutputStream();

            OpusEncodeStream outputStream = new(outStream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);
            _logger.LogDebug("Created OpusEncodeStream for playback.");

            _ = _audioPlaybackService.PlayMp3ToStreamAsync(filePath, outputStream);

            _logger.LogInformation("Started playback of file: {FilePath}", filePath);
            return "Playing test MP3 file!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play MP3 file: {FilePath}", filePath);
            return $"Failed to play MP3: {ex.Message}";
        }
    }

    public async Task<string> StopPlaybackAsync()
    {
        try
        {
            await _audioPlaybackService.StopPlaybackAsync();
            _logger.LogInformation("Playback stopped via universal stop command.");
            return "Playback stopped.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop playback.");
            return $"Failed to stop playback: {ex.Message}";
        }
    }

    private bool IsBotInVoiceChannel(Guild guild, ulong botId)
    {
        return guild.VoiceStates.TryGetValue(botId, out var botVoiceState) && botVoiceState.ChannelId is not null;
    }

    private VoiceState? GetUserVoiceState(Guild guild, ulong userId)
    {
        if (!guild.VoiceStates.TryGetValue(userId, out var voiceState) || voiceState.ChannelId is null)
        {
            return null;
        }
        return voiceState;
    }

    private async Task DisconnectBotFromVoiceChannel(Guild guild, GatewayClient client)
    {
        var emptyChannelVoiceStateProperties = new VoiceStateProperties(guild.Id, null);
        await client.UpdateVoiceStateAsync(emptyChannelVoiceStateProperties);
    }
}