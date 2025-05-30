# Configuration

- Ensure your `appsettings.json` is present in the project root and contains:
  {
    "Discord": {
      "Token": "YOUR_DISCORD_TOKEN_HERE"
    }
  }

- The file must be copied to the output directory. This is handled in the .csproj file.

- You can also set the token using the `DISCORD_TOKEN` environment variable, which takes precedence over the value in `appsettings.json`.

- If you encounter a `Discord token is missing` error, check that:
  - `appsettings.json` is present in the output directory (e.g., `bin/Debug/net9.0/`)
  - The JSON is valid and the token is set
  - The environment variable is set if you prefer that method