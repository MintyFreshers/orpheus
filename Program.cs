using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using System.Diagnostics;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        Debug.WriteLine("Loaded configuration sources:");
        foreach (var provider in ((IConfigurationRoot)configuration).Providers)
        {
            Debug.WriteLine(provider.ToString());
        }
        Debug.WriteLine($"Discord:Token from config: {configuration["Discord:Token"]}");
        Debug.WriteLine($"DISCORD_TOKEN from env: {configuration["DISCORD_TOKEN"]}");

        var token = configuration["DISCORD_TOKEN"] ?? configuration["Discord:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Discord token is missing. Set DISCORD_TOKEN env variable or Discord:Token in appsettings.json.");
        }

        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, configBuilder) =>
            {
                configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                configBuilder.AddEnvironmentVariables();
            })
            .UseDiscordGateway(options =>
            {
                options.Token = token;
            })
            .UseApplicationCommands();

        var host = builder.Build();

        host.AddModules(typeof(Program).Assembly);
        host.UseGatewayEventHandlers();

        await host.RunAsync();
    }
}