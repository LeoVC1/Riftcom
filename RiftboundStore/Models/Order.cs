using System.ComponentModel.DataAnnotations;

namespace RiftboundStore.Models;

public enum DeliveryMethod
{
    UberFlash = 0,
    EventPickup = 1
}

public enum PickupEvent
{
    None = 0,
    MondayCardsHall = 1,
    ThursdayTabletop = 2,
    SaturdayTabletop = 3
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Delivered = 2,
    Cancelled = 3
}

public class Order
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public List<OrderItem> Items { get; set; } = new();

    [Range(0, 100000)]
    [Display(Name = "Doação (R$)")]
    public decimal DonationAmount { get; set; }

    [Display(Name = "Forma de Entrega")]
    public DeliveryMethod DeliveryMethod { get; set; }

    [Display(Name = "Evento para retirada")]
    public PickupEvent PickupEvent { get; set; } = PickupEvent.None;

    [StringLength(500)]
    public string? Notes { get; set; }

    [Display(Name = "Status")]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
