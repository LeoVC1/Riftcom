using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiftboundStore.Data;
using RiftboundStore.Models;

namespace RiftboundStore.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;

    public OrdersController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _config = config;
    }

    private string UserId => _userManager.GetUserId(User)
        ?? throw new InvalidOperationException("User not found.");

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Where(o => o.UserId == UserId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == id && o.UserId == UserId)
            .Include(o => o.Items)
            .FirstOrDefaultAsync();
        if (order is null) return NotFound();

        ViewBag.PixKey = _config["Store:PixKey"] ?? "";
        ViewBag.PixKeyType = _config["Store:PixKeyType"] ?? "";
        ViewBag.WhatsAppUrl = _config["Store:WhatsAppUrl"] ?? "";
        ViewBag.WhatsAppLabel = _config["Store:WhatsAppLabel"] ?? "Contato do administrador";
        return View(order);
    }
}
