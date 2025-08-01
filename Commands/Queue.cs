using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.Queue;

namespace Orpheus.Commands;

public class Queue : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly ISongQueueService _queueService;
    private readonly ILogger<Queue> _logger;

    public Queue(ISongQueueService queueService, ILogger<Queue> logger)
    {
        _queueService = queueService;
        _logger = logger;
    }

    [SlashCommand("queue", "Show the current song queue.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        _logger.LogDebug("Received /queue command from user {UserId}", Context.User.Id);

        var currentSong = _queueService.CurrentSong;
        var queue = _queueService.GetQueue();

        if (currentSong == null && queue.Count == 0)
        {
            await RespondAsync(InteractionCallback.Message("The queue is empty."));
            return;
        }

        var response = "🎵 **Music Queue**\n\n";

        if (currentSong != null)
        {
            response += $"🎶 **Now Playing:** {currentSong.Title}\n";
            response += $"   └ Requested by <@{currentSong.RequestedByUserId}>\n\n";
        }

        if (queue.Count > 0)
        {
            response += "📝 **Up Next:**\n";
            for (int i = 0; i < Math.Min(queue.Count, 10); i++) // Show max 10 items
            {
                var song = queue[i];
                response += $"   {i + 1}. {song.Title}\n";
                response += $"      └ Requested by <@{song.RequestedByUserId}>\n";
            }

            if (queue.Count > 10)
            {
                response += $"   ... and {queue.Count - 10} more songs\n";
            }

            response += $"\n📊 **Total songs in queue:** {queue.Count}";
        }
        else if (currentSong != null)
        {
            response += "📝 **Queue is empty after current song**";
        }

        await RespondAsync(InteractionCallback.Message(response));
    }
}