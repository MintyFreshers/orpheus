using Microsoft.Extensions.Logging;

namespace Orpheus.Services.Transcription;

public class VoiceCommandProcessor : IVoiceCommandProcessor
{
    private readonly ILogger<VoiceCommandProcessor> _logger;

    public VoiceCommandProcessor(ILogger<VoiceCommandProcessor> logger)
    {
        _logger = logger;
    }

    public Task<string> ProcessCommandAsync(string transcription, ulong userId)
    {
        if (string.IsNullOrWhiteSpace(transcription))
        {
            return Task.FromResult("I didn't hear anything.");
        }

        var command = transcription.ToLowerInvariant().Trim();
        _logger.LogInformation("Processing voice command: '{Command}' from user {UserId}", command, userId);

        // Check for "say hello" command
        if (command.Contains("say hello") || command.Contains("say hi"))
        {
            _logger.LogInformation("Recognized 'say hello' command from user {UserId}", userId);
            return Task.FromResult($"<@{userId}> Hello!");
        }

        // If we don't recognize the command
        _logger.LogInformation("Unrecognized command: '{Command}' from user {UserId}", command, userId);
        return Task.FromResult($"<@{userId}> I don't understand.");
    }
}