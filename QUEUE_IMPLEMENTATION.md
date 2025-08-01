# Song Queue Implementation

This document describes the song queue system implementation for the Orpheus Discord bot.

## Overview

The song queue system allows users to add multiple songs to a queue that will play automatically in sequence, rather than having each song immediately replace the previous one.

## Architecture

### Core Components

1. **QueuedSong** - Data model representing a song in the queue
   - Contains title, URL, file path, timestamp, and requester information
   - Automatically assigned unique ID and queue timestamp

2. **ISongQueueService** - Interface for queue management operations
   - Thread-safe queue operations (enqueue, dequeue, peek, clear)
   - Current song tracking
   - Queue state queries

3. **SongQueueService** - In-memory implementation of the queue service
   - Thread-safe using lock synchronization
   - Comprehensive logging of queue operations

4. **IQueuePlaybackService** - Interface for automatic queue processing
   - Manages continuous playback from the queue
   - Handles queue processing lifecycle

5. **QueuePlaybackService** - Implementation of queue playback logic
   - Automatically processes songs from the queue
   - Integrates with existing VoiceClientController for audio playback
   - Supports skip functionality

## Storage Strategy

The implementation uses the existing storage strategy:
- Downloaded songs are stored in the `Downloads/` folder within the Docker container
- This folder is created automatically by the `YouTubeDownloaderService`
- Files are managed by the existing yt-dlp integration

## Commands

### Updated Commands

- **`/play <url>`** - Downloads a YouTube video and adds it to the queue
  - If queue is empty, starts playback immediately
  - If queue has songs, adds to the end and shows position
  - Automatically starts queue processing if not already running

- **`/stop`** - Stops current playback and queue processing
  - Enhanced to stop both individual playback and queue processing

### New Commands

- **`/queue`** - Display the current queue status
  - Shows currently playing song (if any)
  - Shows up to 10 upcoming songs with requester information
  - Displays total queue size

- **`/skip`** - Skip the currently playing song
  - Moves to the next song in the queue
  - Provides feedback about the skipped song

- **`/clearqueue`** - Clear the entire queue and stop playback
  - Removes all queued songs
  - Stops current playback and queue processing
  - Shows count of removed songs

### Unchanged Commands

- **`/playtest`** - Still works independently of the queue system
- **`/download`** - Still downloads without affecting the queue
- **`/join`**, **`/leave`** - Voice channel management unchanged

## Integration with Existing Systems

The queue system integrates seamlessly with existing components:

- **YouTubeDownloaderService** - Used unchanged for downloading audio
- **VoiceClientController** - Used unchanged for audio playback
- **AudioPlaybackService** - Used unchanged for MP3 streaming
- **Dependency Injection** - All queue services registered as singletons

## Thread Safety

All queue operations are thread-safe:
- `SongQueueService` uses lock-based synchronization
- Queue state modifications are atomic
- Safe for concurrent access from multiple Discord command handlers

## Logging

Comprehensive logging throughout the queue system:
- Queue operations (enqueue, dequeue, clear)
- Playback lifecycle events
- Error handling and recovery
- Performance and state tracking

## Error Handling

Robust error handling ensures system stability:
- Download failures don't stop queue processing
- Playback errors automatically move to next song
- Queue state remains consistent during errors
- User feedback for all error conditions

## Future Enhancements

The current implementation provides a solid foundation for future features:
- Shuffle/repeat modes
- Song position manipulation (move, remove specific songs)
- Playlist import/export
- Queue persistence across bot restarts
- Advanced playback controls (pause/resume)