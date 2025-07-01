using AutoClaimFreeSteamGames.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoClaimFreeSteamGames.Services;

public class GameClaimerService : BackgroundService
{
    private readonly ILogger<GameClaimerService> _logger;
    private readonly ISteamDbService _steamDbService;
    private readonly ISteamService _steamService;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _checkInterval;
    private readonly HashSet<string> _claimedGames = new();

    public GameClaimerService(
        ILogger<GameClaimerService> logger,
        ISteamDbService steamDbService,
        ISteamService steamService,
        IConfiguration configuration)
    {
        _logger = logger;
        _steamDbService = steamDbService;
        _steamService = steamService;
        _configuration = configuration;
        
        var intervalMinutes = _configuration.GetValue<int>("SteamSettings:CheckIntervalMinutes", 30);
        _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Steam Free Games Claimer Service gestartet");
        _logger.LogInformation("Prüfintervall: {Interval} Minuten", _checkInterval.TotalMinutes);

        // Initialer Login
        await PerformLoginAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndClaimFreeGamesAsync();
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Service wird beendet...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unerwarteter Fehler im GameClaimerService");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Kurze Pause bei Fehlern
            }
        }

        _steamService.Logout();
        _logger.LogInformation("Steam Free Games Claimer Service beendet");
    }

    private async Task PerformLoginAsync()
    {
        var username = _configuration["SteamSettings:Username"];
        var password = _configuration["SteamSettings:Password"];
        var steamGuardCode = _configuration["SteamSettings:SteamGuardCode"];

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogError("Steam-Credentials nicht in der Konfiguration gefunden!");
            return;
        }

        var loginSuccess = await _steamService.LoginAsync(username, password, steamGuardCode);
        if (!loginSuccess)
        {
            _logger.LogError("Steam-Login fehlgeschlagen! Service wird nicht funktionieren.");
        }
    }

    private async Task CheckAndClaimFreeGamesAsync()
    {
        _logger.LogInformation("Prüfe auf kostenlose Spiele...");

        try
        {
            // Prüfe Login-Status
            if (!await _steamService.IsLoggedInAsync())
            {
                _logger.LogWarning("Nicht bei Steam eingeloggt. Versuche erneuten Login...");
                await PerformLoginAsync();
                return;
            }

            // Hole aktuell kostenlose Spiele
            var freeGames = await _steamDbService.GetCurrentlyFreeGamesAsync();
            _logger.LogInformation("{Count} aktuell kostenlose Spiele gefunden", freeGames.Count);

            if (!freeGames.Any())
            {
                _logger.LogInformation("Keine aktuell kostenlosen Spiele verfügbar");
                return;
            }

            // Zeige gefundene Spiele an
            foreach (var game in freeGames)
            {
                _logger.LogInformation("Gefunden: {Name} (AppID: {AppId}) - {Price}", 
                    game.Name, game.AppId, game.CurrentPrice);
            }

            // Versuche Spiele zu aktivieren
            var claimedCount = 0;
            foreach (var game in freeGames)
            {
                if (_claimedGames.Contains(game.AppId))
                {
                    _logger.LogInformation("Spiel {Name} wurde bereits versucht zu aktivieren", game.Name);
                    continue;
                }

                _logger.LogInformation("Versuche Spiel zu aktivieren: {Name}", game.Name);
                
                var success = await _steamService.ClaimFreeGameAsync(game);
                if (success)
                {
                    claimedCount++;
                    _logger.LogInformation("✅ Spiel erfolgreich aktiviert: {Name}", game.Name);
                }
                else
                {
                    _logger.LogWarning("❌ Spiel konnte nicht aktiviert werden: {Name} - {Error}", 
                        game.Name, game.ClaimError ?? "Unbekannter Fehler");
                }

                _claimedGames.Add(game.AppId);

                // Kurze Pause zwischen Aktivierungen
                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            if (claimedCount > 0)
            {
                _logger.LogInformation("🎉 {Count} Spiele erfolgreich aktiviert!", claimedCount);
            }

            // Zeige anstehende kostenlose Spiele an
            var upcomingGames = await _steamDbService.GetUpcomingFreeGamesAsync();
            if (upcomingGames.Any())
            {
                _logger.LogInformation("Anstehende kostenlose Spiele:");
                foreach (var game in upcomingGames.Take(5)) // Zeige nur die ersten 5
                {
                    var startDate = game.StartDate?.ToString("dd.MM.yyyy HH:mm") ?? "Unbekannt";
                    _logger.LogInformation("  📅 {Name} - Start: {StartDate}", game.Name, startDate);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Prüfen und Aktivieren kostenloser Spiele");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Beende Steam Free Games Claimer Service...");
        await base.StopAsync(cancellationToken);
    }
} 