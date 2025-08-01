# Whisper Voice Commands

This document describes how to use the Whisper transcription feature in Orpheus.

## How It Works

1. **Wake Word Detection**: Say "Orpheus" to activate the bot
2. **Voice Command**: After the bot responds "I'm listening...", you have 5 seconds to say a command
3. **Response**: The bot will transcribe your speech and respond accordingly

## Supported Commands

- **"say hello"** or **"say hi"** → Bot responds with "Hello!"
- **Any other command** → Bot responds with "I don't understand."

## Example Usage

```
User: "Orpheus"
Bot: "<@user> I'm listening..."
User: "say hello"
Bot: "<@user> Hello!"
```

```
User: "Orpheus" 
Bot: "<@user> I'm listening..."
User: "play music"
Bot: "<@user> I don't understand."
```

## Technical Requirements

- The bot must be in a voice channel (use `/join` command)
- Requires Picovoice access key for wake word detection
- Whisper model will be downloaded automatically on first use (~40MB for tiny model)

## Configuration

No additional configuration is needed. The feature uses:
- Existing Picovoice wake word detection
- Whisper tiny model for fast transcription
- 5-second timeout for voice commands after wake word

## Future Enhancements

Additional voice commands can be easily added by modifying the `VoiceCommandProcessor.cs` file.