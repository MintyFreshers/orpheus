using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.VoiceClientController;

namespace Orpheus.Commands;

public class Join : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IVoiceClientController _voiceChannelController;

    public Join(IVoiceClientController voiceChannelController)
    {
        _voiceChannelController = voiceChannelController;
    }

    [SlashCommand("join", "Join a voice channel.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        var guild = Context.Guild!;
        var client = Context.Client;
        var userId = Context.User.Id;
        var resultMessage = await _voiceChannelController.JoinVoiceChannelOfUserAsync(guild, client, userId);
        await RespondAsync(InteractionCallback.Message(resultMessage));
    }
}