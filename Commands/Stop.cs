using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.Queue;
using Orpheus.Services.VoiceClientController;

namespace Orpheus.Commands;

public class Stop : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IVoiceClientController _voiceClientController;
    private readonly IQueuePlaybackService _queuePlaybackService;

    public Stop(IVoiceClientController voiceClientController, IQueuePlaybackService queuePlaybackService)
    {
        _voiceClientController = voiceClientController;
        _queuePlaybackService = queuePlaybackService;
    }

    [SlashCommand("stop", "Stop all playback and queue processing.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        await _queuePlaybackService.StopQueueProcessingAsync();
        var resultMessage = await _voiceClientController.StopPlaybackAsync();
        await RespondAsync(InteractionCallback.Message($"🛑 {resultMessage} Queue processing stopped."));
    }
}