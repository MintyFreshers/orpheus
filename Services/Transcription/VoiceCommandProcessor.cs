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
            return Task.FromResult("I didn't hear anything clearly.");
        }

        var normalizedCommand = transcription.ToLowerInvariant().Trim();
        _logger.LogInformation("Processing voice command: '{Command}' from user {UserId}", normalizedCommand, userId);

        if (IsSayCommand(normalizedCommand))
        {
            var contentToSay = ExtractSayCommandContent(normalizedCommand);
            _logger.LogInformation("Recognized say command from user {UserId}: '{Content}'", userId, contentToSay);
            return Task.FromResult(CreateUserMentionResponse(userId, contentToSay));
        }

        _logger.LogInformation("Unrecognized command: '{Command}' from user {UserId}", normalizedCommand, userId);
        return Task.FromResult(CreateUserMentionResponse(userId, "I don't understand."));
    }



    private static bool IsSayCommand(string normalizedCommand)
    {
        return normalizedCommand.StartsWith("say ") && normalizedCommand.Length > 4;
    }

    private static string ExtractSayCommandContent(string normalizedCommand)
    {
        return normalizedCommand.Substring(4).Trim();
    }

    private static string CreateUserMentionResponse(ulong userId, string message)
    {
        return $"<@{userId}> {message}";
    }
}