using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using Orpheus;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var configuration = BuildConfiguration();
        var token = DiscordTokenProvider.ResolveToken(configuration, out var tokenSource);

        Console.WriteLine($"[Startup] Using Discord token from {tokenSource}: {DiscordTokenProvider.MaskToken(token)}");

        var host = CreateHostBuilder(args, token).Build();

        RegisterModules(host);

        await host.RunAsync();
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static IHostBuilder CreateHostBuilder(string[] args, string token)
    {
        return Host.CreateDefaultBuilder(args)
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
    }

    private static void RegisterModules(IHost host)
    {
        host.AddModules(typeof(Program).Assembly);
        host.UseGatewayEventHandlers();
    }
}