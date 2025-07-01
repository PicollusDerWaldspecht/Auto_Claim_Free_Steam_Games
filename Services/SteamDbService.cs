using AutoClaimFreeSteamGames.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AutoClaimFreeSteamGames.Services;

public class SteamDbService : ISteamDbService
{
    private readonly ILogger<SteamDbService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _steamDbUrl;

    public SteamDbService(ILogger<SteamDbService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _steamDbUrl = configuration["SteamSettings:SteamDbUrl"] ?? "https://steamdb.info/upcoming/free/";
    }

    public async Task<List<SteamGame>> GetFreeGamesAsync()
    {
        try
        {
            _logger.LogInformation("Lade kostenlose Spiele von SteamDB...");
            
            var html = await _httpClient.GetStringAsync(_steamDbUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var games = new List<SteamGame>();
            
            // Suche nach Spielen in der Tabelle
            var gameRows = doc.DocumentNode.SelectNodes("//tr[@data-appid]");
            
            if (gameRows != null)
            {
                foreach (var row in gameRows)
                {
                    try
                    {
                        var game = ParseGameRow(row);
                        if (game != null)
                        {
                            games.Add(game);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Fehler beim Parsen einer Spielzeile");
                    }
                }
            }

            _logger.LogInformation("{Count} kostenlose Spiele gefunden", games.Count);
            return games;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der kostenlosen Spiele von SteamDB");
            return new List<SteamGame>();
        }
    }

    public async Task<List<SteamGame>> GetCurrentlyFreeGamesAsync()
    {
        var allGames = await GetFreeGamesAsync();
        return allGames.Where(g => g.IsCurrentlyFree && g.IsFreeToKeep).ToList();
    }

    public async Task<List<SteamGame>> GetUpcomingFreeGamesAsync()
    {
        var allGames = await GetFreeGamesAsync();
        return allGames.Where(g => !g.IsCurrentlyFree && g.IsFreeToKeep).ToList();
    }

    private SteamGame? ParseGameRow(HtmlNode row)
    {
        try
        {
            var appId = row.GetAttributeValue("data-appid", "");
            if (string.IsNullOrEmpty(appId))
                return null;

            var nameNode = row.SelectSingleNode(".//td[@class='name']//a");
            var name = nameNode?.InnerText.Trim() ?? "";

            var priceNode = row.SelectSingleNode(".//td[@class='price']");
            var currentPrice = priceNode?.InnerText.Trim() ?? "";

            var discountNode = row.SelectSingleNode(".//td[@class='discount']");
            var discount = discountNode?.InnerText.Trim() ?? "";

            var startDateNode = row.SelectSingleNode(".//td[@class='start-date']");
            var startDate = ParseDate(startDateNode?.InnerText.Trim());

            var endDateNode = row.SelectSingleNode(".//td[@class='end-date']");
            var endDate = ParseDate(endDateNode?.InnerText.Trim());

            // Prüfe ob das Spiel "Free to Keep" ist
            var isFreeToKeep = IsFreeToKeepGame(row);
            var isCurrentlyFree = IsCurrentlyFreeGame(row, startDate, endDate);

            return new SteamGame
            {
                AppId = appId,
                Name = name,
                SteamUrl = $"https://store.steampowered.com/app/{appId}",
                CurrentPrice = currentPrice,
                Discount = discount,
                StartDate = startDate,
                EndDate = endDate,
                IsFreeToKeep = isFreeToKeep,
                IsCurrentlyFree = isCurrentlyFree
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Parsen der Spielzeile");
            return null;
        }
    }

    private bool IsFreeToKeepGame(HtmlNode row)
    {
        // Prüfe auf "Free to Keep" Indikatoren
        var priceText = row.SelectSingleNode(".//td[@class='price']")?.InnerText.Trim().ToLower() ?? "";
        var discountText = row.SelectSingleNode(".//td[@class='discount']")?.InnerText.Trim().ToLower() ?? "";
        
        // Spiele sind "Free to Keep" wenn sie 100% Rabatt haben oder "Free" kosten
        return priceText.Contains("free") || discountText.Contains("100%") || discountText.Contains("-100%");
    }

    private bool IsCurrentlyFreeGame(HtmlNode row, DateTime? startDate, DateTime? endDate)
    {
        var now = DateTime.UtcNow;
        
        // Prüfe ob das Spiel aktuell kostenlos ist
        if (startDate.HasValue && endDate.HasValue)
        {
            return now >= startDate.Value && now <= endDate.Value;
        }
        
        // Fallback: Prüfe den Preis
        var priceText = row.SelectSingleNode(".//td[@class='price']")?.InnerText.Trim().ToLower() ?? "";
        return priceText.Contains("free") || priceText.Contains("0");
    }

    private DateTime? ParseDate(string? dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText))
            return null;

        // Entferne Zeitzonen-Informationen und parse das Datum
        dateText = Regex.Replace(dateText, @"\s*\([^)]*\)", "").Trim();
        
        if (DateTime.TryParse(dateText, out var date))
            return date;

        return null;
    }
} 