using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RiftboundStore.Models;
using RiftboundStore.Services;

namespace RiftboundStore.Controllers;

[Authorize]
public class CartController : Controller
{
    private readonly ICartService _cart;
    private readonly UserManager<ApplicationUser> _userManager;

    public CartController(ICartService cart, UserManager<ApplicationUser> userManager)
    {
        _cart = cart;
        _userManager = userManager;
    }

    private string UserId => _userManager.GetUserId(User) ?? throw new InvalidOperationException("User not found.");

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var items = await _cart.GetItemsAsync(UserId);
        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> Add(int cardId, int quantity = 1)
    {
        try
        {
            await _cart.AddAsync(UserId, cardId, quantity);
            TempData["CartMessage"] = "Carta adicionada ao carrinho.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["CartError"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Increment(int cardId, string? returnUrl = null)
    {
        try { await _cart.IncrementAsync(UserId, cardId); }
        catch (InvalidOperationException ex) { TempData["CartError"] = ex.Message; }
        return SafeRedirect(returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Decrement(int cardId, string? returnUrl = null)
    {
        await _cart.DecrementAsync(UserId, cardId);
        return SafeRedirect(returnUrl);
    }

    private IActionResult SafeRedirect(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Update(int cartItemId, int quantity)
    {
        await _cart.UpdateQuantityAsync(UserId, cartItemId, quantity);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Remove(int cartItemId)
    {
        await _cart.RemoveAsync(UserId, cartItemId);
        return RedirectToAction(nameof(Index));
    }
}
