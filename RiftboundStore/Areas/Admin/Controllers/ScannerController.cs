using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiftboundStore.Data;
using RiftboundStore.Models;
using RiftboundStore.Services;

namespace RiftboundStore.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public class ScannerController : Controller
{
    // Maps official Riftbound set prefix → Edition label stored in DB.
    // Keep in sync with the importer / gallery source of truth.
    private static readonly Dictionary<string, string> SetCodeToEdition = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UNL"] = "Unleashed",
        ["OGN"] = "Origins",
        ["SFD"] = "Spiritforged",
        ["OGS"] = "Proving Grounds"
    };

    // Matches things like "UNL-131/219", "OGN-1/294". Language suffix ("EN", "ZH") ignored.
    private static readonly Regex CodeRegex = new(
        @"\b([A-Z]{2,4})-(\d{1,4})(?:/\d{1,4})?\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ApplicationDbContext _db;
    private readonly ILogger<ScannerController> _logger;
    private readonly ICardHasher _hasher;

    public ScannerController(ApplicationDbContext db, ILogger<ScannerController> logger, ICardHasher hasher)
    {
        _db = db;
        _logger = logger;
        _hasher = hasher;
    }

    [HttpGet]
    public IActionResult Index() => View();

    /// <summary>
    /// Given a scanned code (e.g. "UNL-131/219"), return the matching card (or 404).
    /// Client uses this to add a row to the staging list.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Lookup(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return BadRequest(new { error = "Código vazio." });

        var m = CodeRegex.Match(code);
        if (!m.Success) return NotFound(new { error = "Formato inválido.", code });

        var setCode = m.Groups[1].Value.ToUpperInvariant();
        var number = m.Groups[2].Value.TrimStart('0');
        if (string.IsNullOrEmpty(number)) number = "0";

        if (!SetCodeToEdition.TryGetValue(setCode, out var edition))
        {
            return NotFound(new { error = $"Set '{setCode}' desconhecido.", code });
        }

        // Only one row per (Number, Edition) after import (foil derived from rarity, English only).
        var card = await _db.Cards
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Number == number && c.Edition == edition);

        if (card is null)
        {
            return NotFound(new { error = "Carta não cadastrada.", code, setCode, number, edition });
        }

        return Json(new
        {
            id = card.Id,
            name = card.Name,
            number = card.Number,
            edition = card.Edition,
            isFoil = card.IsFoil,
            language = card.Language.ToString(),
            imageUrl = card.ImageUrl,
            currentStock = card.Stock,
            code
        });
    }

    public record ApplyItem(int CardId, int Quantity);
    public record ApplyResult(int Updated, int NotFound, IEnumerable<int> MissingIds);

    /// <summary>
    /// Apply the staged list: increment Stock by the given quantity for each card id.
    /// Idempotency is the caller's problem — this simply adds.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Apply([FromBody] List<ApplyItem> items)
    {
        if (items is null || items.Count == 0)
        {
            return BadRequest(new { error = "Lista vazia." });
        }

        var byId = items
            .Where(i => i.Quantity > 0)
            .GroupBy(i => i.CardId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        if (byId.Count == 0) return BadRequest(new { error = "Nenhum item válido." });

        var ids = byId.Keys.ToList();
        var cards = await _db.Cards.Where(c => ids.Contains(c.Id)).ToListAsync();

        var updated = 0;
        foreach (var card in cards)
        {
            card.Stock += byId[card.Id];
            card.UpdatedAt = DateTime.UtcNow;
            updated++;
        }
        await _db.SaveChangesAsync();

        var missing = ids.Except(cards.Select(c => c.Id)).ToList();
        if (missing.Count > 0)
        {
            _logger.LogWarning("Scanner apply: {Count} card ids not found: {Ids}",
                missing.Count, string.Join(",", missing));
        }

        return Json(new ApplyResult(updated, missing.Count, missing));
    }

    /// <summary>
    /// Return the full card catalog for client-side matching.
    /// Payload is ~150KB for 952 cards — fine to load once per scanner session.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AllCards()
    {
        var rows = await _db.Cards
            .AsNoTracking()
            .Select(c => new
            {
                id = c.Id,
                name = c.Name,
                number = c.Number,
                edition = c.Edition,
                imageUrl = c.ImageUrl,
                isFoil = c.IsFoil,
                currentStock = c.Stock
            })
            .ToListAsync();
        return Json(new { count = rows.Count, cards = rows });
    }

    /// <summary>Return all cards with a computed hash — used by the (legacy) image matcher.</summary>
    [HttpGet]
    public async Task<IActionResult> Hashes()
    {
        var rows = await _db.Cards
            .AsNoTracking()
            .Where(c => c.PerceptualHash != null && c.PerceptualHash != "")
            .Select(c => new { c.Id, c.PerceptualHash, c.Name, c.Number, c.Edition, c.ImageUrl, c.IsFoil, c.Stock })
            .ToListAsync();

        return Json(new
        {
            count = rows.Count,
            cards = rows.Select(r => new
            {
                id = r.Id,
                hash = r.PerceptualHash,
                name = r.Name,
                number = r.Number,
                edition = r.Edition,
                imageUrl = r.ImageUrl,
                isFoil = r.IsFoil,
                currentStock = r.Stock
            })
        });
    }

    public record RecomputeResult(int Total, int Hashed, int Skipped, int Failed);

    /// <summary>
    /// (Re)compute the perceptual hash for every card with an ImageUrl.
    /// Slow (network-bound): ~5-15min for a full 952-card catalog on first run.
    /// Set force=true to also recompute cards that already have a hash.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RecomputeHashes(bool force = false, CancellationToken ct = default)
    {
        var query = _db.Cards.Where(c => c.ImageUrl != null && c.ImageUrl != "");
        if (!force) query = query.Where(c => c.PerceptualHash == null || c.PerceptualHash == "");
        var toProcess = await query.ToListAsync(ct);

        int hashed = 0, failed = 0;
        int total = toProcess.Count;
        _logger.LogInformation("Recompute hashes: {Total} cards to process (force={Force})", total, force);

        // Process in modest parallelism to speed up network I/O without hammering the CDN.
        using var throttle = new SemaphoreSlim(6, 6);
        var tasks = toProcess.Select(async card =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                var hash = await _hasher.ComputeAsync(card.ImageUrl!, ct);
                if (hash is null)
                {
                    Interlocked.Increment(ref failed);
                }
                else
                {
                    card.PerceptualHash = hash;
                    Interlocked.Increment(ref hashed);
                }
            }
            finally { throttle.Release(); }
        }).ToList();

        await Task.WhenAll(tasks);
        await _db.SaveChangesAsync(ct);

        var skipped = await _db.Cards.CountAsync(c => c.PerceptualHash == null || c.PerceptualHash == "", ct);
        return Json(new RecomputeResult(total, hashed, skipped, failed));
    }
}
