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

        var normalizedCommand = transcription.ToLowerInvariant().Trim();
        _logger.LogInformation("Processing voice command: '{Command}' from user {UserId}", normalizedCommand, userId);

        if (IsGreetingCommand(normalizedCommand))
        {
            _logger.LogInformation("Recognized greeting command from user {UserId}", userId);
            return Task.FromResult(CreateUserMentionResponse(userId, "Hello!"));
        }

        _logger.LogInformation("Unrecognized command: '{Command}' from user {UserId}", normalizedCommand, userId);
        return Task.FromResult(CreateUserMentionResponse(userId, "I don't understand."));
    }

    private static bool IsGreetingCommand(string normalizedCommand)
    {
        return normalizedCommand.Contains("say hello") || normalizedCommand.Contains("say hi");
    }

    private static string CreateUserMentionResponse(ulong userId, string message)
    {
        return $"<@{userId}> {message}";
    }
}