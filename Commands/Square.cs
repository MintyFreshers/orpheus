using NetCord.Services.ApplicationCommands;

namespace Orpheus.Commands;

public class Square : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("square", "Get the square of a number")]
    public static string Command(int number)
    {
        return $"{number}² = {number * number}";
    }
}