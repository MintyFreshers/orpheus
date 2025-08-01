# Whisper Voice Commands

This document describes how to use the Whisper transcription feature in Orpheus.

# Whisper Voice Commands

This document describes how to use the Whisper transcription feature in Orpheus.

## How It Works

1. **Continuous Voice Command**: Say "Orpheus" followed immediately by your command (e.g., "Orpheus say hello")
2. **Processing**: The bot will transcribe your complete sentence and respond accordingly
3. **Response**: The bot responds with the appropriate action or message

## Supported Commands

- **"Orpheus say hello"** or **"Orpheus say hi"** → Bot responds with "Hello!"
- **"Orpheus" + any other command** → Bot responds with "I don't understand."

## Example Usage

```
User: "Orpheus say hello"
Bot: "<@user> Hello!"
```

```
User: "Orpheus play music"
Bot: "<@user> I don't understand."
```

## Technical Details

- The bot buffers the last 3 seconds of audio to capture continuous speech
- When "Orpheus" is detected, the buffered audio plus subsequent audio is transcribed
- Commands are processed within an 8-second window after wake word detection
- No need to wait for a response - speak your complete command in one sentence

## Technical Requirements

- The bot must be in a voice channel (use `/join` command)
- Requires Picovoice access key for wake word detection
- Whisper model will be downloaded automatically on first use (~40MB for tiny model)

## Configuration

No additional configuration is needed. The feature uses:
- Existing Picovoice wake word detection
- Whisper tiny model for fast transcription
- 8-second timeout for voice commands after wake word detection
- 3-second audio buffer to capture continuous speech

## Future Enhancements

Additional voice commands can be easily added by modifying the `VoiceCommandProcessor.cs` file.