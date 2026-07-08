using System.ComponentModel.DataAnnotations;

namespace RiftboundStore.Models;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int CardId { get; set; }
    public Card? Card { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; }

    // Snapshot of card at order time (in case card is edited later)
    [Required, StringLength(120)]
    public string CardName { get; set; } = string.Empty;

    [Required, StringLength(32)]
    public string CardNumber { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string CardEdition { get; set; } = string.Empty;

    public CardLanguage CardLanguage { get; set; }

    public bool CardIsFoil { get; set; }
}
