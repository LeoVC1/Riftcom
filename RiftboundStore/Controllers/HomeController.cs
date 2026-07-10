using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiftboundStore.Data;
using RiftboundStore.Models;
using RiftboundStore.Models.ViewModels;
using RiftboundStore.Services;

namespace RiftboundStore.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICartService _cart;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;

    public HomeController(
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

    [HttpGet]
    public IActionResult Contact()
    {
        ViewBag.ContactEmail = _config["Store:ContactEmail"] ?? "";
        ViewBag.WhatsAppPhone = _config["Store:WhatsAppPhone"] ?? "";
        ViewBag.WhatsAppUrl = _config["Store:WhatsAppUrl"] ?? "";
        ViewBag.PixKey = _config["Store:PixKey"] ?? "";
        ViewBag.PixKeyType = _config["Store:PixKeyType"] ?? "";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? q,
        string? edition,
        CardLanguage? language,
        string? rarity,
        string? domain,
        bool? inStockOnly = null,
        string sort = "name",
        int page = 1)
    {
        // Default to true on a fresh visit — the user only sees cards actually available.
        // If the user unchecks the box, the form sends inStockOnly=false via the hidden field trick.
        var effectiveInStockOnly = inStockOnly ?? true;

        var vm = new CardIndexViewModel
        {
            Query = q,
            Edition = edition,
            Language = language,
            Rarity = rarity,
            Domain = domain,
            InStockOnly = effectiveInStockOnly,
            Sort = sort ?? "name",
            Page = page < 1 ? 1 : page
        };

        IQueryable<Card> query = _db.Cards.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.Name, like)
                                     || EF.Functions.Like(c.Number, like));
        }

        if (!string.IsNullOrWhiteSpace(edition))
        {
            query = query.Where(c => c.Edition == edition);
        }

        if (language.HasValue)
        {
            query = query.Where(c => c.Language == language.Value);
        }

        if (!string.IsNullOrWhiteSpace(domain))
        {
            var d = domain.ToLowerInvariant();
            var pattern = $"%,{d},%";
            query = query.Where(c => c.Domains != null && EF.Functions.Like(c.Domains, pattern));
        }

        if (!string.IsNullOrWhiteSpace(rarity))
        {
            var r = rarity.ToLowerInvariant();
            if (r == "other")
            {
                // "Outros" = anything not in the 4 main buckets (includes showcase, unknowns, and future rarities).
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

        if (effectiveInStockOnly)
        {
            query = query.Where(c => c.Stock > 0);
        }

        query = vm.Sort switch
        {
            "newest" => query.OrderByDescending(c => c.CreatedAt),
            "stock" => query.OrderByDescending(c => c.Stock).ThenBy(c => c.Name),
            "edition" => query.OrderBy(c => c.Edition).ThenBy(c => c.Number.Length).ThenBy(c => c.Number),
            "number_asc" => query.OrderBy(c => c.Number.Length).ThenBy(c => c.Number),
            "number_desc" => query.OrderByDescending(c => c.Number.Length).ThenByDescending(c => c.Number),
            _ => query.OrderBy(c => c.Name)
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

        var uid = _userManager.GetUserId(User);
        if (!string.IsNullOrEmpty(uid))
        {
            vm.CartQuantities = await _cart.GetQuantitiesAsync(uid);
        }

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Card(int id)
    {
        var card = await _db.Cards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (card is null) return NotFound();
        return View(card);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
