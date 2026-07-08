namespace RiftboundStore.Models.ViewModels;

public class CardIndexViewModel
{
    public string? Query { get; set; }
    public string? Edition { get; set; }
    public CardLanguage? Language { get; set; }
    // One of: "common", "uncommon", "rare", "epic", "other" (aggregates anything unmapped).
    public string? Rarity { get; set; }
    public bool InStockOnly { get; set; }
    public string Sort { get; set; } = "name";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public IReadOnlyList<Card> Cards { get; set; } = Array.Empty<Card>();
    public IReadOnlyList<string> Editions { get; set; } = Array.Empty<string>();
    public Dictionary<int, int> CartQuantities { get; set; } = new();
}
