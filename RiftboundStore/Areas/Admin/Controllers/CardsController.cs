using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiftboundStore.Data;
using RiftboundStore.Models;
using RiftboundStore.Models.ViewModels;

namespace RiftboundStore.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public class CardsController : Controller
{
    private static readonly string[] AllowedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long MaxImageBytes = 4 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CardsController(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IActionResult> Index(
        string? q,
        string? edition,
        CardLanguage? language,
        string? rarity,
        bool? foil,
        bool inStockOnly = false,
        string sort = "edition",
        int page = 1)
    {
        var vm = new AdminCardsIndexViewModel
        {
            Query = q,
            Edition = edition,
            Language = language,
            Rarity = rarity,
            Foil = foil,
            InStockOnly = inStockOnly,
            Sort = sort ?? "edition",
            Page = page < 1 ? 1 : page
        };

        IQueryable<Card> query = _db.Cards.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.Name, like)
                                     || EF.Functions.Like(c.Number, like)
                                     || EF.Functions.Like(c.Edition, like));
        }

        if (!string.IsNullOrWhiteSpace(edition))
            query = query.Where(c => c.Edition == edition);

        if (language.HasValue)
            query = query.Where(c => c.Language == language.Value);

        if (foil.HasValue)
            query = query.Where(c => c.IsFoil == foil.Value);

        if (inStockOnly)
            query = query.Where(c => c.Stock > 0);

        if (!string.IsNullOrWhiteSpace(rarity))
        {
            var r = rarity.ToLowerInvariant();
            if (r == "other")
            {
                query = query.Where(c => c.Rarity == null
                                          || (c.Rarity != DisplayNames.RarityCommon
                                              && c.Rarity != DisplayNames.RarityUncommon
                                              && c.Rarity != DisplayNames.RarityRare
                                              && c.Rarity != DisplayNames.RarityEpic));
            }
            else
            {
                query = query.Where(c => c.Rarity == r);
            }
        }

        query = vm.Sort switch
        {
            "name" => query.OrderBy(c => c.Name),
            "number_asc" => query.OrderBy(c => c.Number.Length).ThenBy(c => c.Number),
            "number_desc" => query.OrderByDescending(c => c.Number.Length).ThenByDescending(c => c.Number),
            "stock_desc" => query.OrderByDescending(c => c.Stock).ThenBy(c => c.Name),
            "stock_asc" => query.OrderBy(c => c.Stock).ThenBy(c => c.Name),
            "recent" => query.OrderByDescending(c => c.UpdatedAt),
            _ /* edition */ => query.OrderBy(c => c.Edition).ThenBy(c => c.Number.Length).ThenBy(c => c.Number)
        };

        vm.TotalCount = await query.CountAsync();
        vm.Cards = await query
            .Skip((vm.Page - 1) * vm.PageSize)
            .Take(vm.PageSize)
            .ToListAsync();

        vm.Editions = await _db.Cards
            .AsNoTracking()
            .Select(c => c.Edition)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();

        return View(vm);
    }

    [HttpGet]
    public IActionResult Create() => View(new Card { Language = CardLanguage.English });

    [HttpPost]
    public async Task<IActionResult> Create(Card model, IFormFile? image)
    {
        if (!ModelState.IsValid) return View(model);

        if (image is { Length: > 0 })
        {
            var url = await SaveImageAsync(image);
            if (url is null)
            {
                ModelState.AddModelError("image", "Formato inválido ou arquivo grande demais (máx 4MB).");
                return View(model);
            }
            model.ImageUrl = url;
        }

        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;
        _db.Cards.Add(model);
        await _db.SaveChangesAsync();
        TempData["AdminMessage"] = "Carta criada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == id);
        if (card is null) return NotFound();
        return View(card);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Card model, IFormFile? image)
    {
        if (id != model.Id) return BadRequest();
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == id);
        if (card is null) return NotFound();

        if (!ModelState.IsValid) return View(model);

        if (image is { Length: > 0 })
        {
            var url = await SaveImageAsync(image);
            if (url is null)
            {
                ModelState.AddModelError("image", "Formato inválido ou arquivo grande demais (máx 4MB).");
                return View(model);
            }
            card.ImageUrl = url;
        }
        else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            card.ImageUrl = model.ImageUrl;
        }

        card.Name = model.Name;
        card.Number = model.Number;
        card.Edition = model.Edition;
        card.Language = model.Language;
        card.IsFoil = model.IsFoil;
        card.Stock = model.Stock;
        card.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["AdminMessage"] = "Carta atualizada.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Inline stock editor from the Cards list. If <paramref name="delta"/> is provided
    /// (from clicking +/-), apply it to the current stock. Otherwise use <paramref name="newStock"/>
    /// directly (from typing a value and pressing Enter).
    ///
    /// Progressive enhancement: returns JSON if the client sent `Accept: application/json`
    /// (JS-enhanced flow that avoids the scroll-to-top). Otherwise falls back to a redirect
    /// so the plain-form path still works if JS is off.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UpdateStock(int id, int? newStock, int? delta, string? q, int page = 1)
    {
        var wantsJson = Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == id);
        if (card is null)
        {
            return wantsJson
                ? NotFound(new { error = "Carta não encontrada." })
                : NotFound();
        }

        int target;
        if (delta.HasValue) target = card.Stock + delta.Value;
        else if (newStock.HasValue) target = newStock.Value;
        else
        {
            return wantsJson
                ? Json(new { ok = true, stock = card.Stock, message = "Nada a alterar." })
                : RedirectToAction(nameof(Index), new { q, page });
        }

        if (target < 0) target = 0;
        if (target > 10000) target = 10000;

        var changed = target != card.Stock;
        if (changed)
        {
            card.Stock = target;
            card.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        if (wantsJson)
        {
            return Json(new
            {
                ok = true,
                stock = card.Stock,
                message = changed
                    ? $"'{card.Name}' — estoque agora {card.Stock}."
                    : "Sem mudanças."
            });
        }

        if (changed)
        {
            TempData["AdminMessage"] = $"'{card.Name}' — estoque agora {card.Stock}.";
        }
        return RedirectToAction(nameof(Index), new { q, page });
    }

    /// <summary>
    /// Inline foil toggle from the Cards list. Same progressive-enhancement pattern as UpdateStock.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UpdateFoil(int id, bool isFoil, string? q, int page = 1)
    {
        var wantsJson = Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == id);
        if (card is null)
        {
            return wantsJson ? NotFound(new { error = "Carta não encontrada." }) : NotFound();
        }

        var changed = card.IsFoil != isFoil;
        if (changed)
        {
            card.IsFoil = isFoil;
            card.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        if (wantsJson)
        {
            return Json(new
            {
                ok = true,
                isFoil = card.IsFoil,
                message = changed
                    ? $"'{card.Name}' — {(card.IsFoil ? "marcada" : "desmarcada")} como Foil."
                    : "Sem mudanças."
            });
        }

        if (changed)
        {
            TempData["AdminMessage"] = $"'{card.Name}' — Foil = {(card.IsFoil ? "Sim" : "Não")}.";
        }
        return RedirectToAction(nameof(Index), new { q, page });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == id);
        if (card is null) return NotFound();

        var inOrders = await _db.OrderItems.AnyAsync(i => i.CardId == id);
        if (inOrders)
        {
            TempData["AdminError"] = "Carta em uso por pedidos existentes. Zere o estoque em vez de deletar.";
            return RedirectToAction(nameof(Index));
        }
        _db.Cards.Remove(card);
        await _db.SaveChangesAsync();
        TempData["AdminMessage"] = "Carta removida.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> SaveImageAsync(IFormFile image)
    {
        if (image.Length <= 0 || image.Length > MaxImageBytes) return null;
        var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext)) return null;

        var folder = Path.Combine(_env.WebRootPath, "images", "cards");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(folder, fileName);
        using var fs = System.IO.File.Create(full);
        await image.CopyToAsync(fs);
        return $"/images/cards/{fileName}";
    }
}
