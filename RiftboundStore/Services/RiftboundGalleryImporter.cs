using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RiftboundStore.Data;
using RiftboundStore.Models;

namespace RiftboundStore.Services;

public interface IRiftboundGalleryImporter
{
    Task<ImportResult> ImportAsync(CancellationToken ct = default);
}

public record ImportResult(int Fetched, int Created, int Updated, int Skipped, IReadOnlyList<string> Warnings);

public class RiftboundGalleryImporter : IRiftboundGalleryImporter
{
    private const string GalleryUrl = "https://playriftbound.com/en-us/card-gallery/";
    private static readonly HashSet<string> FoilRarities = new(StringComparer.OrdinalIgnoreCase)
    {
        "rare", "epic", "showcase"
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RiftboundGalleryImporter> _logger;

    public RiftboundGalleryImporter(
        IHttpClientFactory httpFactory,
        ApplicationDbContext db,
        ILogger<RiftboundGalleryImporter> logger)
    {
        _httpFactory = httpFactory;
        _db = db;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; RiftcomImporter/1.0)");
        http.Timeout = TimeSpan.FromSeconds(60);

        _logger.LogInformation("Baixando galeria oficial em {Url}", GalleryUrl);
        var html = await http.GetStringAsync(GalleryUrl, ct);

        var items = ExtractItems(html);
        _logger.LogInformation("Encontradas {Count} cartas na galeria.", items.Count);

        var warnings = new List<string>();
        int created = 0, updated = 0, skipped = 0;

        foreach (var src in items)
        {
            ct.ThrowIfCancellationRequested();

            var name = src.Name?.Trim();
            var number = src.CollectorNumber?.ToString();
            var edition = src.Set?.Value?.Label?.Trim();
            var rarityId = src.Rarity?.Value?.Id ?? string.Empty;
            var imageUrl = src.CardImage?.Url;

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(number) ||
                string.IsNullOrWhiteSpace(edition))
            {
                warnings.Add($"Ignorada: dados incompletos (id={src.Id})");
                skipped++;
                continue;
            }

            var isFoil = FoilRarities.Contains(rarityId);
            const CardLanguage language = CardLanguage.English;

            var existing = await _db.Cards.FirstOrDefaultAsync(c =>
                c.Number == number &&
                c.Edition == edition &&
                c.Language == language &&
                c.IsFoil == isFoil, ct);

            if (existing is null)
            {
                _db.Cards.Add(new Card
                {
                    Name = name,
                    Number = number,
                    Edition = edition,
                    Language = language,
                    IsFoil = isFoil,
                    Stock = 0,
                    ImageUrl = imageUrl,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                created++;
            }
            else
            {
                var changed = false;
                if (existing.Name != name) { existing.Name = name; changed = true; }
                if (existing.ImageUrl != imageUrl) { existing.ImageUrl = imageUrl; changed = true; }
                // Stock is intentionally preserved.
                if (changed)
                {
                    existing.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
                else
                {
                    skipped++;
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        return new ImportResult(items.Count, created, updated, skipped, warnings);
    }

    private static List<CardDto> ExtractItems(string html)
    {
        const string marker = "\"cards\":{\"items\":[";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException(
                "Bloco de cartas não encontrado no HTML — a página pode ter mudado.");
        }
        var arrayStart = start + marker.Length - 1; // include the '['
        var end = FindMatchingBracket(html, arrayStart);
        var json = html.Substring(arrayStart, end - arrayStart + 1);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        var items = JsonSerializer.Deserialize<List<CardDto>>(json, options);
        if (items is null || items.Count == 0)
        {
            throw new InvalidOperationException("Nenhuma carta pôde ser decodificada do HTML.");
        }
        return items;
    }

    /// <summary>Given the index of '[' returns the index of the matching ']', respecting strings.</summary>
    private static int FindMatchingBracket(string s, int openIndex)
    {
        int depth = 0;
        bool inStr = false;
        bool esc = false;
        for (int i = openIndex; i < s.Length; i++)
        {
            char c = s[i];
            if (esc) { esc = false; continue; }
            if (c == '\\' && inStr) { esc = true; continue; }
            if (c == '"') { inStr = !inStr; continue; }
            if (inStr) continue;
            if (c == '[') depth++;
            else if (c == ']')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        throw new InvalidOperationException("Colchete de fechamento não encontrado.");
    }

    // --- JSON DTOs (only the fields we care about) ---
    private class CardDto
    {
        public string? Id { get; set; }
        public int? CollectorNumber { get; set; }
        public string? PublicCode { get; set; }
        public string? Name { get; set; }
        public SetDto? Set { get; set; }
        public RarityDto? Rarity { get; set; }
        public ImageDto? CardImage { get; set; }
    }
    private class SetDto { public SetValueDto? Value { get; set; } }
    private class SetValueDto { public string? Id { get; set; } public string? Label { get; set; } }
    private class RarityDto { public RarityValueDto? Value { get; set; } }
    private class RarityValueDto { public string? Id { get; set; } public string? Label { get; set; } }
    private class ImageDto { public string? Url { get; set; } }
}
