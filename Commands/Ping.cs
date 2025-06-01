using NetCord.Services.ApplicationCommands;

namespace Orpheus.Commands;

public class Ping : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("ping", "Ping!")]
    public static string Command()
    {
        return "Pong!";
    }
}