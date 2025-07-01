using AutoClaimFreeSteamGames.Models;

namespace AutoClaimFreeSteamGames.Services;

public interface ISteamService
{
    Task<bool> LoginAsync(string username, string password, string? steamGuardCode = null);
    Task<bool> IsLoggedInAsync();
    Task<List<SteamGame>> GetOwnedGamesAsync();
    Task<bool> ClaimFreeGameAsync(SteamGame game);
    Task<bool> CheckIfGameIsOwnedAsync(string appId);
    void Logout();
} 