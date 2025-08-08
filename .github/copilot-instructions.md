# Orpheus Discord Music Bot

Orpheus is a feature-rich Discord music bot built with .NET 8.0 and the NetCord library. It supports YouTube music playback with queue management, voice commands using Whisper transcription, and wake word detection with Picovoice.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively

### Bootstrap and Build
- Install required system dependencies:
  - `sudo apt update && sudo apt install -y ffmpeg python3-pip`
  - `pip3 install --user yt-dlp` -- installs YouTube downloader tool
- Restore .NET dependencies:
  - `dotnet restore` -- takes ~1 second if up-to-date, ~23 seconds first time. NEVER CANCEL. Set timeout to 60+ seconds.
- Build the project:
  - `dotnet build --no-restore` -- takes ~1.5 seconds incremental, ~10 seconds clean build. NEVER CANCEL. Set timeout to 30+ seconds.
- Clean and rebuild if needed:
  - `dotnet clean && dotnet restore && dotnet build` -- takes ~3 seconds total after first run. NEVER CANCEL. Set timeout to 90+ seconds.

### Configuration Setup
- Copy example configuration:
  - `cp Config/appsettings.example.json Config/appsettings.json`
- Set Discord bot token (REQUIRED):
  - Environment variable: `export DISCORD_TOKEN="your_discord_bot_token_here"`
  - OR edit `Config/appsettings.json` and replace `<YOUR_DISCORD_BOT_TOKEN_HERE>` with actual token
- Set Picovoice access key (OPTIONAL - for voice commands):
  - Edit `Config/appsettings.json` and replace `<YOUR_PICOVOICE_ACCESS_KEY_HERE>` with actual key
- Environment variables take precedence over config file values

### Run the Application
- ALWAYS run the bootstrapping steps first (dependencies + build)
- Local development: `dotnet run` -- starts immediately if configured properly
- With custom token: `DISCORD_TOKEN="your_token" dotnet run`
- The bot will display masked token on startup: `Using Discord token from [source]: abcd...efgh`

### Docker Deployment
- Build Docker image: `docker build --no-cache -t orpheus .` -- takes 3-5 minutes. NEVER CANCEL. Set timeout to 10+ minutes.
- NOTE: Docker build may fail in sandboxed environments due to SSL certificate issues with PyPI
- Run container: `docker run -e DISCORD_TOKEN=your_token_here orpheus`
- The Dockerfile automatically installs ffmpeg, python3, and yt-dlp in a virtual environment

## Testing and Validation

### Build Validation
- ALWAYS test basic build process: `dotnet clean && dotnet restore && dotnet build`
- Build should complete with 0 warnings and 0 errors
- Expected output: "Build succeeded" with timing information

### Runtime Validation
- Test configuration loading: `dotnet run` (should show token source and exit gracefully or start bot)
- Without token: Should display clear error message about missing Discord token
- With invalid token format: Should display NetCord validation error

### Manual Validation Scenarios
- ALWAYS test the Discord bot functionality after making changes to command handlers
- **Basic Commands**: Test `/ping` command (should respond with "Pong!")
- **Music Commands**: Test `/play https://www.youtube.com/watch?v=example` (requires valid Discord token and voice channel)
- **Voice Commands**: Say "Orpheus say hello" while bot is in voice channel (requires Picovoice key)
- **Queue Management**: Test `/queue`, `/skip`, `/clearqueue` commands

### External Dependencies Verification
- Verify ffmpeg: `ffmpeg -version` -- should show version 6.1.1 or later
- Verify yt-dlp: `yt-dlp --version` -- should show current version (2025.07.21 or later)
- Verify Python: `python3 --version` -- should show Python 3.12 or later

## Architecture Overview

### Key Components
- **Program.cs**: Main entry point with dependency injection setup
- **Commands/**: Discord slash command handlers (`/play`, `/ping`, `/queue`, etc.)
- **Services/**: Core business logic (queue management, audio playback, transcription)
- **Utils/**: Helper utilities (token resolution, etc.)
- **Configuration/**: Bot configuration classes

### Core Services
- **ISongQueueService**: Thread-safe song queue management
- **IQueuePlaybackService**: Automatic queue processing and playback
- **IYouTubeDownloader**: YouTube video download using yt-dlp
- **ITranscriptionService**: Whisper-based voice transcription
- **IVoiceClientController**: Discord voice channel management

### Discord Commands
- **Basic**: `/ping`, `/join`, `/leave`, `/stop`
- **Playback**: `/play <url-or-search>`, `/playtest`, `/resume`
- **Queue Management**: `/queue`, `/skip`, `/clearqueue`, `/playnext`
- **Voice Commands**: "Orpheus say hello" (requires wake word detection)

### File Locations
- **Configuration**: `Config/appsettings.json` (copied from `Config/appsettings.example.json`)
- **Resources**: `Resources/ExampleTrack.mp3`, `Resources/orpheus_keyword_file.ppn`
- **Build Output**: `bin/Debug/net8.0/` (includes all dependencies and resources)
- **Documentation**: `README.md`, `QUEUE_IMPLEMENTATION.md`, `VOICE_COMMANDS.md`

## Common Tasks

### Adding New Commands
- Create new class in `Commands/` directory inheriting from `ApplicationCommandModule<ApplicationCommandContext>`
- Use `[SlashCommand("name", "description")]` attribute
- Register automatically via dependency injection in `Program.cs`

### Modifying Services
- All services use dependency injection - modify interfaces in respective service folders
- Service registration occurs in `Program.ConfigureServices()` method
- Services are registered as singletons for thread safety

### Debugging Audio Issues
- Check ffmpeg installation: `which ffmpeg && ffmpeg -version`
- Check yt-dlp functionality: `yt-dlp --version && yt-dlp --extract-audio --audio-format mp3 "test_url"`
- Audio files downloaded to `Downloads/` folder within application directory
- Enable debug logging by building in Debug configuration

### Configuration Troubleshooting
- Token errors: Ensure Discord token is valid bot token (not user token)
- Missing config: Verify `Config/appsettings.json` exists and is copied to build output
- Picovoice errors: Voice commands require valid Picovoice access key or will be disabled

## Development Notes

### No Tests Available
- This project currently has no unit tests or integration tests
- Manual testing is required for all functionality
- Always test Discord bot functionality with real Discord server after changes

### No CI/CD Pipeline
- No `.github/workflows/` directory exists
- Manual build and deployment process only
- Consider adding GitHub Actions workflows for automated testing

### Performance Expectations
- **Restore**: ~1 second if up-to-date, ~23 seconds first time
- **Build**: ~1.5 seconds incremental, ~10 seconds clean build
- **Startup**: <2 seconds with valid configuration
- **Docker Build**: 3-10 minutes depending on network and system performance (may fail in sandboxed environments due to SSL issues)

### Known Limitations
- Docker builds may fail in environments with SSL certificate restrictions
- Voice commands require Picovoice subscription for wake word detection
- YouTube downloads require stable internet connection
- Bot requires "Connect" and "Speak" permissions in Discord voice channels

## Security Notes
- Never commit actual Discord tokens or API keys to version control
- Use environment variables or local configuration files for secrets
- The `Config/appsettings.example.json` file shows the required structure without real credentials
- Always use the example file as template and create local `appsettings.json` for development