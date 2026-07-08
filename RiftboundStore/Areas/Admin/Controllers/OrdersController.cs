using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiftboundStore.Data;
using RiftboundStore.Models;

namespace RiftboundStore.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _db;

    public OrdersController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(OrderStatus? status)
    {
        IQueryable<Order> query = _db.Orders
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.Items);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var list = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        ViewBag.StatusFilter = status;
        return View(list);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.Items).ThenInclude(i => i.Card)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        return View(order);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, string? returnTo = null)
    {
        var wantsJson = Request.Headers.Accept.ToString()
            .Contains("application/json", StringComparison.OrdinalIgnoreCase);

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
        {
            return wantsJson ? NotFound(new { error = "Pedido não encontrado." }) : NotFound();
        }

        var changed = order.Status != status;
        if (changed)
        {
            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        if (wantsJson)
        {
            return Json(new
            {
                ok = true,
                status = (int)order.Status,
                statusName = order.Status.ToString(),
                statusLabel = DisplayNames.ForStatus(order.Status),
                statusBadgeClass = DisplayNames.StatusBadgeClass(order.Status),
                message = changed
                    ? $"Pedido #{id}: {DisplayNames.ForStatus(order.Status)}."
                    : "Sem mudanças."
            });
        }

        if (changed) TempData["AdminMessage"] = $"Pedido #{id}: {DisplayNames.ForStatus(order.Status)}.";
        return returnTo == "index"
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Details), new { id });
    }
}
