using AutoClaimFreeSteamGames.Models;

namespace AutoClaimFreeSteamGames.Services;

public interface ISteamDbService
{
    Task<List<SteamGame>> GetFreeGamesAsync();
    Task<List<SteamGame>> GetCurrentlyFreeGamesAsync();
    Task<List<SteamGame>> GetUpcomingFreeGamesAsync();
} 