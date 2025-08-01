using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.Queue;

namespace Orpheus.Commands;

public class Resume : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly ISongQueueService _queueService;
    private readonly IQueuePlaybackService _queuePlaybackService;
    private readonly ILogger<Resume> _logger;

    public Resume(
        ISongQueueService queueService,
        IQueuePlaybackService queuePlaybackService,
        ILogger<Resume> logger)
    {
        _queueService = queueService;
        _queuePlaybackService = queuePlaybackService;
        _logger = logger;
    }

    [SlashCommand("resume", "Resume queue processing and start playing songs from the queue.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        var guild = Context.Guild!;
        var client = Context.Client;
        var userId = Context.User.Id;

        _logger.LogInformation("Received /resume command from user {UserId} in guild {GuildId}", userId, guild.Id);

        try
        {
            if (_queuePlaybackService.IsProcessing)
            {
                await RespondAsync(InteractionCallback.Message("🎵 Queue processing is already running!"));
                return;
            }

            if (_queueService.IsEmpty && _queueService.CurrentSong == null)
            {
                await RespondAsync(InteractionCallback.Message("❌ No songs in queue to resume playing."));
                return;
            }

            await _queuePlaybackService.StartQueueProcessingAsync(guild, client, userId);
            
            var queueCount = _queueService.Count;
            var message = queueCount > 0 
                ? $"▶️ Resumed queue processing! {queueCount} song(s) in queue." 
                : "▶️ Resumed queue processing!";

            await RespondAsync(InteractionCallback.Message(message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in /resume command");
            await RespondAsync(InteractionCallback.Message("An error occurred while resuming queue processing."));
        }
    }
}