using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiftboundStore.Data;
using RiftboundStore.Models;
using RiftboundStore.Models.ViewModels;
using RiftboundStore.Services;

namespace RiftboundStore.Controllers;

[Authorize]
public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICartService _cart;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;

    public CheckoutController(
        ApplicationDbContext db,
        ICartService cart,
        UserManager<ApplicationUser> userManager,
        IConfiguration config)
    {
        _db = db;
        _cart = cart;
        _userManager = userManager;
        _config = config;
    }

    private string UserId => _userManager.GetUserId(User) ?? throw new InvalidOperationException("User not found.");

    private CheckoutViewModel BuildViewModel(IReadOnlyList<CartItem> items, CheckoutViewModel? existing = null)
    {
        var vm = existing ?? new CheckoutViewModel();
        vm.Items = items;
        vm.PixKey = _config["Store:PixKey"] ?? "";
        vm.PixKeyType = _config["Store:PixKeyType"] ?? "";
        vm.WhatsAppUrl = _config["Store:WhatsAppUrl"] ?? "";
        vm.WhatsAppLabel = _config["Store:WhatsAppLabel"] ?? "Contato do administrador";
        return vm;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var items = await _cart.GetItemsAsync(UserId);
        if (!items.Any())
        {
            TempData["CartMessage"] = "Seu carrinho está vazio.";
            return RedirectToAction("Index", "Cart");
        }
        return View(BuildViewModel(items));
    }

    [HttpPost]
    public async Task<IActionResult> Confirm(CheckoutViewModel model)
    {
        var items = await _cart.GetItemsAsync(UserId);
        if (!items.Any())
        {
            TempData["CartError"] = "Seu carrinho está vazio.";
            return RedirectToAction("Index", "Cart");
        }

        if (model.DeliveryMethod == DeliveryMethod.EventPickup && model.PickupEvent == PickupEvent.None)
        {
            ModelState.AddModelError(nameof(model.PickupEvent), "Escolha um evento para retirada.");
        }

        if (model.DonationPreset == "other" && (!model.DonationCustom.HasValue || model.DonationCustom.Value < 0))
        {
            ModelState.AddModelError(nameof(model.DonationCustom), "Informe um valor válido para a doação.");
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), BuildViewModel(items, model));
        }

        // Recheck stock and adjust quantities
        foreach (var it in items)
        {
            if (it.Card is null || it.Card.Stock <= 0)
            {
                ModelState.AddModelError(string.Empty, $"Carta '{it.Card?.Name}' está sem estoque.");
                return View(nameof(Index), BuildViewModel(items, model));
            }
            if (it.Quantity > it.Card.Stock)
            {
                it.Quantity = it.Card.Stock;
            }
        }

        var donation = Math.Round(model.ComputedDonation(), 2);
        if (donation < 0) donation = 0;

        var order = new Order
        {
            UserId = UserId,
            DonationAmount = donation,
            DeliveryMethod = model.DeliveryMethod,
            PickupEvent = model.DeliveryMethod == DeliveryMethod.EventPickup ? model.PickupEvent : PickupEvent.None,
            Notes = model.Notes,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = items.Select(i => new OrderItem
            {
                CardId = i.CardId,
                Quantity = i.Quantity,
                CardName = i.Card!.Name,
                CardNumber = i.Card.Number,
                CardEdition = i.Card.Edition,
                CardLanguage = i.Card.Language,
                CardIsFoil = i.Card.IsFoil
            }).ToList()
        };

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Decrement stock
            foreach (var i in items)
            {
                var card = await _db.Cards.FirstAsync(c => c.Id == i.CardId);
                if (card.Stock < i.Quantity)
                {
                    throw new InvalidOperationException($"Estoque insuficiente para {card.Name}.");
                }
                card.Stock -= i.Quantity;
                card.UpdatedAt = DateTime.UtcNow;
            }

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            await _cart.ClearAsync(UserId);
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(nameof(Index), BuildViewModel(items, model));
        }

        return RedirectToAction(nameof(Confirmation), new { id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == UserId);
        if (order is null) return NotFound();

        ViewBag.PixKey = _config["Store:PixKey"] ?? "";
        ViewBag.PixKeyType = _config["Store:PixKeyType"] ?? "";
        ViewBag.WhatsAppUrl = _config["Store:WhatsAppUrl"] ?? "";
        ViewBag.WhatsAppLabel = _config["Store:WhatsAppLabel"] ?? "Contato do administrador";
        return View(order);
    }
}
