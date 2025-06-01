using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Logging;
using Microsoft.Extensions.Logging;
using NetCord.Rest;

namespace Orpheus.Services.VoiceClientController;

public class VoiceClientController : IVoiceClientController
{
    private readonly ILogger<VoiceClientController> _logger;
    private readonly string _instanceGuid;
    private VoiceClient? _voiceClient;

    public VoiceClientController(ILogger<VoiceClientController> logger)
    {
        _logger = logger;
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