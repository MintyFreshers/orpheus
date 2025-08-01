using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.VoiceClientController;

namespace Orpheus.Commands;

public class Leave : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IVoiceClientController _voiceClientController;

    public Leave(IVoiceClientController voiceClientController)
    {
        _voiceClientController = voiceClientController;
    }

    [SlashCommand("leave", "Leave the voice channel.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        var guild = Context.Guild!;
        var client = Context.Client;
        var resultMessage = await _voiceClientController.LeaveVoiceChannelAsync(guild, client);
        await RespondAsync(InteractionCallback.Message(resultMessage));
    }
}
