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

        var cleanedCommand = RemoveWakeWordPrefix(normalizedCommand);

        if (IsGreetingCommand(cleanedCommand))
        {
            _logger.LogInformation("Recognized greeting command from user {UserId}", userId);
            return Task.FromResult(CreateUserMentionResponse(userId, "Hello!"));
        }

        _logger.LogInformation("Unrecognized command: '{Command}' from user {UserId}", cleanedCommand, userId);
        return Task.FromResult(CreateUserMentionResponse(userId, "I don't understand."));
    }

    private static string RemoveWakeWordPrefix(string normalizedCommand)
    {
        var wakeWordVariations = new[] { "orpheus", "orfeus", "orphius" };
        
        foreach (var wakeWord in wakeWordVariations)
        {
            if (normalizedCommand.StartsWith(wakeWord))
            {
                var commandWithoutWakeWord = normalizedCommand.Substring(wakeWord.Length).Trim();
                return commandWithoutWakeWord;
            }
        }

        return normalizedCommand;
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