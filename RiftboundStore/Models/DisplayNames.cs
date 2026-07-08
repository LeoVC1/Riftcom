namespace RiftboundStore.Models;

public static class DisplayNames
{
    public static string ForStatus(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Pendente",
        OrderStatus.Confirmed => "Separado",
        OrderStatus.Delivered => "Finalizado",
        OrderStatus.Cancelled => "Cancelado",
        _ => status.ToString()
    };

    public static string StatusBadgeClass(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "text-bg-warning",
        OrderStatus.Confirmed => "text-bg-info",
        OrderStatus.Delivered => "text-bg-success",
        OrderStatus.Cancelled => "text-bg-secondary",
        _ => "text-bg-light"
    };

    public static string ForDelivery(DeliveryMethod method) => method switch
    {
        DeliveryMethod.EventPickup => "Retirada em evento",
        DeliveryMethod.UberFlash => "Uber Flash",
        _ => method.ToString()
    };

    public static string ForPickup(PickupEvent ev) => ev switch
    {
        PickupEvent.MondayCardsHall => "Segunda-feira 19h (CardsHall)",
        PickupEvent.ThursdayTabletop => "Quinta-feira 19h30 (Tabletop)",
        PickupEvent.SaturdayTabletop => "Sábado 14h30 (Tabletop)",
        _ => "—"
    };

    // Well-known source rarity IDs from playriftbound.com.
    public const string RarityCommon = "common";
    public const string RarityUncommon = "uncommon";
    public const string RarityRare = "rare";
    public const string RarityEpic = "epic";
    public static readonly string[] MainRarities = { RarityCommon, RarityUncommon, RarityRare, RarityEpic };

    public static string ForRarity(string? rarity) => (rarity ?? string.Empty).ToLowerInvariant() switch
    {
        RarityCommon => "Comum",
        RarityUncommon => "Incomum",
        RarityRare => "Raro",
        RarityEpic => "Épico",
        "" or null => "—",
        _ => "Outros"
    };
}
