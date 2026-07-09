namespace RiftboundStore.Models.ViewModels;

public class AdminCardsIndexViewModel
{
    public string? Query { get; set; }
    public string? Edition { get; set; }
    public CardLanguage? Language { get; set; }
    // Multi-select: any subset of "common" | "uncommon" | "rare" | "epic" | "other".
    // Empty set = no rarity filter (matches all).
    public HashSet<string> Rarities { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool? Foil { get; set; }
    public bool InStockOnly { get; set; }
    public string Sort { get; set; } = "edition";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public IReadOnlyList<Card> Cards { get; set; } = Array.Empty<Card>();
    public IReadOnlyList<string> Editions { get; set; } = Array.Empty<string>();
}
