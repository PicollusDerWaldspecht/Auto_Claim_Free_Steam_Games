using AutoClaimFreeSteamGames.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

namespace AutoClaimFreeSteamGames.Services;

public class SteamService : ISteamService
{
    private readonly ILogger<SteamService> _logger;
    private readonly HttpClient _httpClient;
    private string? _sessionId;
    private string? _steamLoginSecure;
    private bool _isLoggedIn;

    public SteamService(ILogger<SteamService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<bool> LoginAsync(string username, string password, string? steamGuardCode = null)
    {
        try
        {
            _logger.LogInformation("Versuche Steam-Login...");

            // Erstelle Login-Request
            var loginData = new Dictionary<string, string>
            {
                ["username"] = username,
                ["password"] = password,
                ["remember_login"] = "true",
                ["rsatimestamp"] = await GetRsaTimestampAsync(),
                ["captchagid"] = "-1"
            };

            if (!string.IsNullOrEmpty(steamGuardCode))
            {
                loginData["twofactorcode"] = steamGuardCode;
            }

            var content = new FormUrlEncodedContent(loginData);
            var response = await _httpClient.PostAsync("https://steamcommunity.com/login/dologin/", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            var loginResult = JsonConvert.DeserializeObject<SteamLoginResponse>(responseContent);

            if (loginResult?.Success == true)
            {
                // Extrahiere Session-Cookies
                var cookies = response.Headers.GetValues("Set-Cookie");
                foreach (var cookie in cookies)
                {
                    if (cookie.Contains("sessionid="))
                    {
                        _sessionId = ExtractCookieValue(cookie, "sessionid");
                    }
                    if (cookie.Contains("steamLoginSecure="))
                    {
                        _steamLoginSecure = ExtractCookieValue(cookie, "steamLoginSecure");
                    }
                }

                _isLoggedIn = true;
                _logger.LogInformation("Steam-Login erfolgreich!");
                return true;
            }
            else
            {
                _logger.LogError("Steam-Login fehlgeschlagen: {Message}", loginResult?.Message);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Steam-Login");
            return false;
        }
    }

    public async Task<bool> IsLoggedInAsync()
    {
        if (!_isLoggedIn || string.IsNullOrEmpty(_sessionId))
            return false;

        try
        {
            var response = await _httpClient.GetAsync("https://steamcommunity.com/my/");
            return response.IsSuccessStatusCode && response.Content.ReadAsStringAsync().Result.Contains("profile_header_centered_persona");
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<SteamGame>> GetOwnedGamesAsync()
    {
        var games = new List<SteamGame>();
        
        try
        {
            if (!await IsLoggedInAsync())
            {
                _logger.LogWarning("Nicht bei Steam eingeloggt");
                return games;
            }

            var response = await _httpClient.GetAsync("https://steamcommunity.com/my/games/?tab=all");
            var content = await response.Content.ReadAsStringAsync();

            // Parse owned games from Steam profile
            // Dies ist eine vereinfachte Implementierung
            // In der Praxis müsstest du das HTML parsen oder die Steam API verwenden

            _logger.LogInformation("Eigene Spiele geladen");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der eigenen Spiele");
        }

        return games;
    }

    public async Task<bool> ClaimFreeGameAsync(SteamGame game)
    {
        try
        {
            if (!await IsLoggedInAsync())
            {
                _logger.LogWarning("Nicht bei Steam eingeloggt");
                return false;
            }

            // Prüfe ob das Spiel bereits besessen wird
            if (await CheckIfGameIsOwnedAsync(game.AppId))
            {
                _logger.LogInformation("Spiel {GameName} wird bereits besessen", game.Name);
                game.AlreadyOwned = true;
                return true;
            }

            _logger.LogInformation("Versuche Spiel zu aktivieren: {GameName} (AppID: {AppId})", game.Name, game.AppId);

            // Navigiere zur Spielseite und aktiviere es
            var gameUrl = $"https://store.steampowered.com/app/{game.AppId}";
            var response = await _httpClient.GetAsync(gameUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Konnte Spielseite nicht laden: {StatusCode}", response.StatusCode);
                return false;
            }

            // Suche nach dem "Add to Account" Button und klicke ihn
            var content = await response.Content.ReadAsStringAsync();
            
            // Extrahiere die notwendigen Parameter für den Kauf
            var purchaseParams = ExtractPurchaseParameters(content, game.AppId);
            
            if (purchaseParams != null)
            {
                var purchaseResponse = await PurchaseGameAsync(purchaseParams);
                if (purchaseResponse)
                {
                    _logger.LogInformation("Spiel erfolgreich aktiviert: {GameName}", game.Name);
                    game.SuccessfullyClaimed = true;
                    return true;
                }
            }

            _logger.LogWarning("Konnte Spiel nicht aktivieren: {GameName}", game.Name);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Aktivieren des Spiels: {GameName}", game.Name);
            game.ClaimError = ex.Message;
            return false;
        }
    }

    public async Task<bool> CheckIfGameIsOwnedAsync(string appId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"https://store.steampowered.com/app/{appId}");
            var content = await response.Content.ReadAsStringAsync();
            
            // Prüfe ob "Add to Account" oder "Play Now" Button vorhanden ist
            return content.Contains("Add to Account") || content.Contains("Play Now");
        }
        catch
        {
            return false;
        }
    }

    public void Logout()
    {
        _sessionId = null;
        _steamLoginSecure = null;
        _isLoggedIn = false;
        _logger.LogInformation("Steam-Logout durchgeführt");
    }

    private async Task<string> GetRsaTimestampAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("https://steamcommunity.com/login/getrsakey/");
            var content = await response.Content.ReadAsStringAsync();
            var rsaResponse = JsonConvert.DeserializeObject<SteamRsaResponse>(content);
            return rsaResponse?.Timestamp ?? "";
        }
        catch
        {
            return "";
        }
    }

    private string? ExtractCookieValue(string cookie, string name)
    {
        var match = System.Text.RegularExpressions.Regex.Match(cookie, $"{name}=([^;]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private Dictionary<string, string>? ExtractPurchaseParameters(string content, string appId)
    {
        // Extrahiere die notwendigen Parameter für den Kauf
        // Dies ist eine vereinfachte Implementierung
        var params = new Dictionary<string, string>
        {
            ["appid"] = appId,
            ["purchase_type"] = "gift",
            ["snr"] = "1_4_4__125"
        };

        return params;
    }

    private async Task<bool> PurchaseGameAsync(Dictionary<string, string> parameters)
    {
        try
        {
            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync("https://store.steampowered.com/checkout/addfreelicense/", content);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<SteamPurchaseResponse>(responseContent);
            
            return result?.Success == true;
        }
        catch
        {
            return false;
        }
    }
}

// Response-Klassen für Steam API
public class SteamLoginResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    
    [JsonProperty("message")]
    public string? Message { get; set; }
}

public class SteamRsaResponse
{
    [JsonProperty("timestamp")]
    public string? Timestamp { get; set; }
}

public class SteamPurchaseResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    
    [JsonProperty("message")]
    public string? Message { get; set; }
} 