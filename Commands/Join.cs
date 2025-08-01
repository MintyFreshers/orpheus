using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.VoiceClientController;

namespace Orpheus.Commands;

public class Join : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IVoiceClientController _voiceClientController;

    public Join(IVoiceClientController voiceClientController)
    {
        _voiceClientController = voiceClientController;
    }

    [SlashCommand("join", "Join a voice channel.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        var guild = Context.Guild!;
        var client = Context.Client;
        var userId = Context.User.Id;
        var resultMessage = await _voiceClientController.JoinVoiceChannelOfUserAsync(guild, client, userId);
        await RespondAsync(InteractionCallback.Message(resultMessage));
    }
}