using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using Orpheus.Configuration;
using Orpheus.Services;
using Orpheus.Services.Downloader.Youtube;
using Orpheus.Services.Queue;
using Orpheus.Services.VoiceClientController;
using Orpheus.Services.WakeWord;
using Orpheus.Services.Transcription;
using Orpheus.Utils;

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
            .AddJsonFile("Config/appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static IHostBuilder CreateHostBuilder(string[] args, string token)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, configBuilder) =>
            {
                configBuilder.AddJsonFile("Config/appsettings.json", optional: true, reloadOnChange: true);
                configBuilder.AddEnvironmentVariables();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
#if DEBUG
                logging.SetMinimumLevel(LogLevel.Debug);
#endif
            })
            .ConfigureServices(ConfigureServices)
            .UseDiscordGateway(options =>
            {
                options.Token = token;
            })
            .UseApplicationCommands();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<IYouTubeDownloader, YouTubeDownloaderService>();
        services.AddSingleton<ISongQueueService, SongQueueService>();
        services.AddSingleton<IQueuePlaybackService, QueuePlaybackService>();
        services.AddSingleton<BackgroundDownloadService>();
        services.AddSingleton<IBackgroundDownloadService>(provider => provider.GetRequiredService<BackgroundDownloadService>());
        services.AddHostedService<BackgroundDownloadService>(provider => provider.GetRequiredService<BackgroundDownloadService>());
        services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        services.AddSingleton<IVoiceClientController, VoiceClientController>();
        services.AddSingleton<IWakeWordDetectionService, PicovoiceWakeWordService>();
        services.AddSingleton<ITranscriptionService, WhisperTranscriptionService>();
        services.AddSingleton<IVoiceCommandProcessor, VoiceCommandProcessor>();
        services.AddSingleton<BotConfiguration>();
        services.AddSingleton<WakeWordResponseHandler>();
        services.AddSingleton<IMessageUpdateService, MessageUpdateService>();
    }

    private static void RegisterModules(IHost host)
    {
        host.AddModules(typeof(Program).Assembly);
        host.UseGatewayEventHandlers();
        
        // Initialize transcription service
        var transcriptionService = host.Services.GetRequiredService<ITranscriptionService>();
        _ = transcriptionService.InitializeAsync();
    }
}