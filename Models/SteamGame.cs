namespace AutoClaimFreeSteamGames.Models;

public class SteamGame
{
    public string Name { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string SteamUrl { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsFreeToKeep { get; set; }
    public bool IsCurrentlyFree { get; set; }
    public string? OriginalPrice { get; set; }
    public string? CurrentPrice { get; set; }
    public string? Discount { get; set; }
    public string? ImageUrl { get; set; }
    public bool AlreadyOwned { get; set; }
    public bool SuccessfullyClaimed { get; set; }
    public string? ClaimError { get; set; }
} 