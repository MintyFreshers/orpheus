using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using Orpheus.Configuration;

namespace Orpheus.Services.WakeWord;

public class WakeWordResponseHandler
{
    private readonly ILogger<WakeWordResponseHandler> _logger;
    private readonly BotConfiguration _discordConfiguration;

    public WakeWordResponseHandler(ILogger<WakeWordResponseHandler> logger, BotConfiguration discordConfiguration)
    {
        _logger = logger;
        _discordConfiguration = discordConfiguration;
    }

    public async Task HandleWakeWordDetectionAsync(ulong userId, GatewayClient? client)
    {
        if (client == null)
        {
            _logger.LogWarning("Cannot send wake word response: Gateway client is null");
            return;
        }

        try
        {
            var channelId = _discordConfiguration.DefaultChannelId;
            var mentionMessage = CreateWakeWordResponseMessage(userId);

            _logger.LogInformation("Wake word detected from user {UserId}", userId);
            await client.Rest.SendMessageAsync(channelId, mentionMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send wake word response message");
        }
    }

    private MessageProperties CreateWakeWordResponseMessage(ulong userId)
    {
        return new MessageProperties()
            .WithContent($"<@{userId}> What do you want?");
    }
}
