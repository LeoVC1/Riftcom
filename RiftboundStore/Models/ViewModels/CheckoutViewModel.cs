using System.ComponentModel.DataAnnotations;

namespace RiftboundStore.Models.ViewModels;

public class CheckoutViewModel
{
    public IReadOnlyList<CartItem> Items { get; set; } = Array.Empty<CartItem>();
    public int TotalQuantity => Items.Sum(i => i.Quantity);

    // Donation
    [Display(Name = "Ajudar a manutenção da plataforma")]
    public string DonationPreset { get; set; } = "none"; // "none", "2", "5", "10", "20", "other"

    [Range(0, 100000, ErrorMessage = "Valor inválido.")]
    [Display(Name = "Outro valor (R$)")]
    public decimal? DonationCustom { get; set; }

    // Delivery
    [Display(Name = "Forma de entrega")]
    public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.EventPickup;

    [Display(Name = "Evento para retirada")]
    public PickupEvent PickupEvent { get; set; } = PickupEvent.MondayCardsHall;

    [StringLength(500)]
    public string? Notes { get; set; }

    // Read-only PIX info from configuration
    public string PixKey { get; set; } = string.Empty;
    public string PixKeyType { get; set; } = string.Empty;
    public string WhatsAppUrl { get; set; } = string.Empty;
    public string WhatsAppLabel { get; set; } = string.Empty;

    public decimal ComputedDonation()
    {
        return DonationPreset switch
        {
            "2" => 2m,
            "5" => 5m,
            "10" => 10m,
            "20" => 20m,
            "other" => DonationCustom ?? 0m,
            _ => 0m
        };
    }
}
