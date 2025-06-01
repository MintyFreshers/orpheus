using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.VoiceClientController;

namespace Orpheus.Commands;

public class Echo : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IVoiceClientController _voiceChannelController;

    public Echo(IVoiceClientController voiceChannelController)
    {
        _voiceChannelController = voiceChannelController;
    }

    [SlashCommand("echo", "Repeat what everyone is saying.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        var guild = Context.Guild!;
        var client = Context.Client;
        var userId = Context.User.Id;
        var resultMessage = await _voiceChannelController.StartEchoingAsync(guild, client, userId);
        await RespondAsync(InteractionCallback.Message(resultMessage));
    }
}