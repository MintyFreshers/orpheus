namespace Orpheus.Services.Transcription;

public interface ITranscriptionService
{
    Task<string?> TranscribeAudioAsync(byte[] audioData);
    Task InitializeAsync();
    bool IsInitialized { get; }
    void Cleanup();
}