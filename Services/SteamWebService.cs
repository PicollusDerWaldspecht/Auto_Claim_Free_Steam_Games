using AutoClaimFreeSteamGames.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoClaimFreeSteamGames.Services;

public class SteamWebService : ISteamService
{
    private readonly ILogger<SteamWebService> _logger;
    private readonly HttpClient _httpClient;
    private string? _sessionId;
    private string? _steamLoginSecure;
    private string? _steamCountry;
    private bool _isLoggedIn;

    public SteamWebService(ILogger<SteamWebService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "de-DE,de;q=0.8,en-US;q=0.5,en;q=0.3");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        _httpClient.DefaultRequestHeaders.Add("DNT", "1");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    }

    public async Task<bool> LoginAsync(string username, string password, string? steamGuardCode = null)
    {
        try
        {
            _logger.LogInformation("Versuche Steam-Login...");

            // Hole RSA Public Key
            var rsaResponse = await GetRsaPublicKeyAsync();
            if (rsaResponse == null)
            {
                _logger.LogError("Konnte RSA Public Key nicht abrufen");
                return false;
            }

            // Erstelle Login-Request
            var loginData = new Dictionary<string, string>
            {
                ["username"] = username,
                ["password"] = password,
                ["rsatimestamp"] = rsaResponse.Timestamp,
                ["remember_login"] = "true",
                ["captchagid"] = "-1",
                ["captcha_text"] = "",
                ["emailauth"] = "",
                ["emailsteamid"] = "",
                ["rsatimestamp"] = rsaResponse.Timestamp,
                ["donotcache"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
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
                // Extrahiere Cookies
                await ExtractCookiesAsync(response);
                _isLoggedIn = true;
                _logger.LogInformation("Steam-Login erfolgreich!");
                return true;
            }
            else
            {
                _logger.LogError("Steam-Login fehlgeschlagen: {Message}", loginResult?.Message);
                
                // Prüfe auf spezielle Fehler
                if (loginResult?.RequiresTwoFactor == true)
                {
                    _logger.LogWarning("Steam Guard Code erforderlich");
                }
                else if (loginResult?.RequiresCaptcha == true)
                {
                    _logger.LogWarning("CAPTCHA erforderlich - nicht unterstützt");
                }
                
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
        if (!_isLoggedIn)
            return false;

        try
        {
            var response = await _httpClient.GetAsync("https://steamcommunity.com/my/");
            var content = await response.Content.ReadAsStringAsync();
            
            // Prüfe auf Login-Indikatoren
            return content.Contains("profile_header_centered_persona") || 
                   content.Contains("g_steamID") ||
                   !content.Contains("login");
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

            // Parse owned games using regex
            var gameMatches = Regex.Matches(content, @"""appid"":(\d+),""name"":""([^""]+)""");
            
            foreach (Match match in gameMatches)
            {
                if (match.Groups.Count >= 3)
                {
                    games.Add(new SteamGame
                    {
                        AppId = match.Groups[1].Value,
                        Name = match.Groups[2].Value,
                        AlreadyOwned = true
                    });
                }
            }

            _logger.LogInformation("{Count} eigene Spiele gefunden", games.Count);
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

            // Navigiere zur Spielseite
            var gameUrl = $"https://store.steampowered.com/app/{game.AppId}";
            var response = await _httpClient.GetAsync(gameUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Konnte Spielseite nicht laden: {StatusCode}", response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            
            // Prüfe ob das Spiel kostenlos ist
            if (!IsGameFree(content))
            {
                _logger.LogWarning("Spiel {GameName} ist nicht kostenlos", game.Name);
                return false;
            }

            // Extrahiere Session-Parameter
            var sessionParams = ExtractSessionParameters(content);
            if (sessionParams == null)
            {
                _logger.LogError("Konnte Session-Parameter nicht extrahieren");
                return false;
            }

            // Versuche das Spiel zu aktivieren
            var success = await AddFreeGameToAccountAsync(game.AppId, sessionParams);
            if (success)
            {
                _logger.LogInformation("Spiel erfolgreich aktiviert: {GameName}", game.Name);
                game.SuccessfullyClaimed = true;
                return true;
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
            
            // Prüfe auf "Add to Account" oder "Play Now" Button
            return content.Contains("Add to Account") || 
                   content.Contains("Play Now") ||
                   content.Contains("In Library") ||
                   content.Contains("Install Now");
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
        _steamCountry = null;
        _isLoggedIn = false;
        _logger.LogInformation("Steam-Logout durchgeführt");
    }

    private async Task<SteamRsaResponse?> GetRsaPublicKeyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("https://steamcommunity.com/login/getrsakey/");
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<SteamRsaResponse>(content);
        }
        catch
        {
            return null;
        }
    }

    private async Task ExtractCookiesAsync(HttpResponseMessage response)
    {
        if (response.Headers.Contains("Set-Cookie"))
        {
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
                if (cookie.Contains("steamCountry="))
                {
                    _steamCountry = ExtractCookieValue(cookie, "steamCountry");
                }
            }
        }
    }

    private string? ExtractCookieValue(string cookie, string name)
    {
        var match = Regex.Match(cookie, $"{name}=([^;]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private bool IsGameFree(string content)
    {
        // Prüfe auf kostenlose Indikatoren
        return content.Contains("Free to Play") ||
               content.Contains("Free") ||
               content.Contains("0,00") ||
               content.Contains("$0.00") ||
               content.Contains("€0.00");
    }

    private Dictionary<string, string>? ExtractSessionParameters(string content)
    {
        try
        {
            var params = new Dictionary<string, string>();
            
            // Extrahiere g_sessionID
            var sessionMatch = Regex.Match(content, @"g_sessionID\s*=\s*['""]([^'""]+)['""]");
            if (sessionMatch.Success)
            {
                params["sessionid"] = sessionMatch.Groups[1].Value;
            }

            // Extrahiere andere notwendige Parameter
            var snrMatch = Regex.Match(content, @"snr\s*=\s*['""]([^'""]+)['""]");
            if (snrMatch.Success)
            {
                params["snr"] = snrMatch.Groups[1].Value;
            }

            return params.Count > 0 ? params : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> AddFreeGameToAccountAsync(string appId, Dictionary<string, string> sessionParams)
    {
        try
        {
            var purchaseData = new Dictionary<string, string>
            {
                ["appid"] = appId,
                ["purchase_type"] = "gift",
                ["snr"] = sessionParams.GetValueOrDefault("snr", "1_4_4__125"),
                ["sessionid"] = sessionParams.GetValueOrDefault("sessionid", ""),
                ["donotcache"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };

            var content = new FormUrlEncodedContent(purchaseData);
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
    
    [JsonProperty("requires_twofactor")]
    public bool RequiresTwoFactor { get; set; }
    
    [JsonProperty("requires_captcha")]
    public bool RequiresCaptcha { get; set; }
    
    [JsonProperty("captcha_gid")]
    public string? CaptchaGid { get; set; }
}

public class SteamRsaResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    
    [JsonProperty("publickey_mod")]
    public string? PublicKeyMod { get; set; }
    
    [JsonProperty("publickey_exp")]
    public string? PublicKeyExp { get; set; }
    
    [JsonProperty("timestamp")]
    public string? Timestamp { get; set; }
}

public class SteamPurchaseResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    
    [JsonProperty("message")]
    public string? Message { get; set; }
    
    [JsonProperty("purchase_result_details")]
    public int PurchaseResultDetails { get; set; }
} 