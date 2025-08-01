using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Orpheus.Services.VoiceClientController;

namespace Orpheus.Commands;

public class PlayTest : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IVoiceClientController _voiceClientController;
    private readonly ILogger<PlayTest> _logger;
    private const string TestFilePath = "Resources/ExampleTrack.mp3";

    public PlayTest(IVoiceClientController voiceClientController, ILogger<PlayTest> logger)
    {
        _voiceClientController = voiceClientController;
        _logger = logger;
    }

    [SlashCommand("playtest", "Play a test MP3 file in your voice channel.", Contexts = [InteractionContextType.Guild])]
    public async Task Command()
    {
        _logger.LogDebug("Received /playtest command. Checking for file: {FilePath}", TestFilePath);

        if (!File.Exists(TestFilePath))
        {
            _logger.LogWarning("MP3 file not found at path: {FilePath}", TestFilePath);
            await RespondAsync(InteractionCallback.Message($"File not found: {TestFilePath}"));
            return;
        }

        var guild = Context.Guild!;
        var client = Context.Client;
        var userId = Context.User.Id;

        _logger.LogInformation("Attempting to play MP3 file '{FilePath}' for user {UserId} in guild {GuildId}",
            TestFilePath, userId, guild.Id);

        var resultMessage = await _voiceClientController.PlayMp3Async(guild, client, userId, TestFilePath);

        if (resultMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Failed to play MP3: {Message}", resultMessage);
        }
        else
        {
            _logger.LogInformation("PlayTest result: {Message}", resultMessage);
        }

        await RespondAsync(InteractionCallback.Message(resultMessage));
    }
}