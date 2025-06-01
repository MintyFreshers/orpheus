using Microsoft.Extensions.Configuration;

namespace Orpheus.Utils;

public static class DiscordTokenProvider
{
    public static string ResolveToken(IConfiguration configuration, out string tokenSource)
    {
        var envToken = configuration["DISCORD_TOKEN"];
        var configToken = configuration["Discord:Token"];

        if (!string.IsNullOrWhiteSpace(envToken))
        {
            tokenSource = "environment variable DISCORD_TOKEN";
            return envToken;
        }

        if (!string.IsNullOrWhiteSpace(configToken))
        {
            tokenSource = "appsettings.json (Discord:Token)";
            return configToken;
        }

        throw new InvalidOperationException("Discord token is missing. Set DISCORD_TOKEN env variable or Discord:Token in appsettings.json.");
    }

    public static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 8)
        {
            return "****";
        }
        return $"{token.Substring(0, 4)}...{token.Substring(token.Length - 4)}";
    }
}