using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.VoiceClientController;
using Microsoft.Extensions.Logging;

namespace Orpheus.Commands;

public class PlayTest : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IVoiceClientController _voiceChannelController;
    private readonly ILogger<PlayTest> _logger;

    public PlayTest(IVoiceClientController voiceChannelController, ILogger<PlayTest> logger)
    {
        _voiceChannelController = voiceChannelController;
        _logger = logger;
    }

    [SlashCommand("playtest", "Play a test MP3 file in your voice channel.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        var filePath = "Resources/ExampleTrack.mp3";
        _logger.LogDebug("Received /playtest command. Checking for file: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("MP3 file not found at path: {FilePath}", filePath);
            await RespondAsync(InteractionCallback.Message($"File not found: {filePath}"));
            return;
        }

        var guild = Context.Guild!;
        var client = Context.Client;
        var userId = Context.User.Id;
        _logger.LogInformation("Attempting to play MP3 file '{FilePath}' for user {UserId} in guild {GuildId}", filePath, userId, guild.Id);

        var resultMessage = await _voiceChannelController.PlayMp3Async(guild, client, userId, filePath);

        if (resultMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            _logger.LogError("Failed to play MP3: {Message}", resultMessage);
        else
            _logger.LogInformation("PlayTest result: {Message}", resultMessage);

        await RespondAsync(InteractionCallback.Message(resultMessage));
    }
}