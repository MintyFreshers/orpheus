using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.VoiceClientController;

namespace Orpheus.Commands;

public class Leave : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IVoiceClientController _voiceChannelController;

    public Leave(IVoiceClientController voiceChannelController)
    {
        _voiceChannelController = voiceChannelController;
    }

    [SlashCommand("leave", "Leave the voice channel.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        var guild = Context.Guild!;
        var client = Context.Client;
        var resultMessage = await _voiceChannelController.LeaveVoiceChannelAsync(guild, client);
        await RespondAsync(InteractionCallback.Message(resultMessage));
    }
}
