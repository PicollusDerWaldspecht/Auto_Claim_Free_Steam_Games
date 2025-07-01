using AutoClaimFreeSteamGames.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoClaimFreeSteamGames;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        if (args.Contains("--console"))
        {
            // Als Konsolenanwendung ausführen
            await host.RunAsync();
        }
        else
        {
            // Als Windows Service ausführen
            await host.RunAsync();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "Steam Free Games Claimer";
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Services registrieren
                services.AddSingleton<ISteamDbService, SteamDbService>();
                services.AddSingleton<ISteamService, SteamService>();
                services.AddHostedService<GameClaimerService>();
                
                // Logging konfigurieren
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddFile("logs/steam-claimer-{Date}.log");
                });
            })
            .ConfigureAppConfiguration((hostContext, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true);
                config.AddEnvironmentVariables();
            });
} 