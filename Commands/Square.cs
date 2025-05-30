using NetCord.Services.ApplicationCommands;

namespace Orpheus.Commands;

public class Square : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("square", "Get the square")]
    public static string Command(int num) => $"{num}² = {num * num}";
}