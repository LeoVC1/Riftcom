using System.ComponentModel.DataAnnotations;

namespace RiftboundStore.Models;

public enum CardLanguage
{
    English = 0,
    Chinese = 1
}

public class Card
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(32)]
    [Display(Name = "Número")]
    public string Number { get; set; } = string.Empty;

    [Required, StringLength(80)]
    [Display(Name = "Edição")]
    public string Edition { get; set; } = string.Empty;

    [Display(Name = "Idioma")]
    public CardLanguage Language { get; set; } = CardLanguage.English;

    [Display(Name = "Foil")]
    public bool IsFoil { get; set; }

    // Source rarity ID from playriftbound.com: "common", "uncommon", "rare", "epic", "showcase", ...
    // Kept as string so future rarities the game adds don't require a schema migration.
    [StringLength(32)]
    [Display(Name = "Raridade")]
    public string? Rarity { get; set; }

    // Comma-delimited list of source domain IDs with leading/trailing commas for LIKE matching.
    // Format: ",chaos,order," or ",colorless,". Null when no domain.
    [StringLength(128)]
    [Display(Name = "Domínio")]
    public string? Domains { get; set; }

    [Range(0, 10000)]
    [Display(Name = "Estoque")]
    public int Stock { get; set; }

    [StringLength(400)]
    [Display(Name = "URL da Imagem")]
    public string? ImageUrl { get; set; }

    // 64-bit dHash of the card art, hex-encoded (16 chars). Used by the webcam scanner
    // to match a captured frame against known cards.
    [StringLength(16)]
    public string? PerceptualHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
