namespace Orpheus.Services.Transcription;

public interface IVoiceCommandProcessor
{
    Task<string> ProcessCommandAsync(string transcription, ulong userId);
}