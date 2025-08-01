namespace Orpheus.Services.WakeWord;

public interface IWakeWordDetectionService
{
    void Initialize();
    bool IsInitialized { get; }
    bool ProcessAudioFrame(byte[] opusFrame, ulong userId);
    void Cleanup();
    event Action<ulong>? WakeWordDetected;
}