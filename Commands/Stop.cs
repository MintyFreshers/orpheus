using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.VoiceClientController;

namespace Orpheus.Commands;

public class Stop : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IVoiceClientController _voiceClientController;

    public Stop(IVoiceClientController voiceClientController)
    {
        _voiceClientController = voiceClientController;
    }

    [SlashCommand("stop", "Stop all playback and related audio actions.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        var resultMessage = await _voiceClientController.StopPlaybackAsync();
        await RespondAsync(InteractionCallback.Message(resultMessage));
    }
}