using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.VoiceClientController;

namespace Orpheus.Commands;

public class Stop : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IVoiceClientController _voiceChannelController;

    public Stop(IVoiceClientController voiceChannelController)
    {
        _voiceChannelController = voiceChannelController;
    }

    [SlashCommand("stop", "Stop all playback and related audio actions.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        var resultMessage = await _voiceChannelController.StopPlaybackAsync();
        await RespondAsync(InteractionCallback.Message(resultMessage));
    }
}