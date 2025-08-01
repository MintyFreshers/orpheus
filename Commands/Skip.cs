using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.Queue;

namespace Orpheus.Commands;

public class Skip : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IQueuePlaybackService _queuePlaybackService;
    private readonly ISongQueueService _queueService;
    private readonly ILogger<Skip> _logger;

    public Skip(IQueuePlaybackService queuePlaybackService, ISongQueueService queueService, ILogger<Skip> logger)
    {
        _queuePlaybackService = queuePlaybackService;
        _queueService = queueService;
        _logger = logger;
    }

    [SlashCommand("skip", "Skip the currently playing song.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        _logger.LogInformation("Received /skip command from user {UserId}", Context.User.Id);

        var currentSong = _queueService.CurrentSong;
        if (currentSong == null)
        {
            await RespondAsync(InteractionCallback.Message("No song is currently playing."));
            return;
        }

        try
        {
            await _queuePlaybackService.SkipCurrentSongAsync();
            await RespondAsync(InteractionCallback.Message($"⏭️ Skipped **{currentSong.Title}**"));
            _logger.LogInformation("Skipped song: {Title}", currentSong.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error skipping song: {Title}", currentSong.Title);
            await RespondAsync(InteractionCallback.Message("Failed to skip the current song."));
        }
    }
}