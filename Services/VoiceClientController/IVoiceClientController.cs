using NetCord.Gateway;
using NetCord.Rest;

namespace Orpheus.Services.VoiceClientController;

public interface IVoiceClientController
{
    Task<string> JoinVoiceChannelOfUserAsync(Guild guild, GatewayClient client, ulong userId);
    Task<string> LeaveVoiceChannelAsync(Guild guild, GatewayClient client);
    Task<string> StartEchoingAsync(Guild guild, GatewayClient client, ulong userId);
}