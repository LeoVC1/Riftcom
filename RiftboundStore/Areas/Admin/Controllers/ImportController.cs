using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiftboundStore.Services;

namespace RiftboundStore.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public class ImportController : Controller
{
    private readonly IRiftboundGalleryImporter _importer;
    private readonly ILogger<ImportController> _logger;

    public ImportController(IRiftboundGalleryImporter importer, ILogger<ImportController> logger)
    {
        _importer = importer;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        try
        {
            var result = await _importer.ImportAsync(ct);
            TempData["ImportSummary"] =
                $"Cartas encontradas: {result.Fetched} · Criadas: {result.Created} · Atualizadas: {result.Updated} · Sem mudanças: {result.Skipped}";
            if (result.Warnings.Count > 0)
            {
                TempData["ImportWarnings"] = string.Join("\n", result.Warnings.Take(50));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao importar galeria.");
            TempData["AdminError"] = "Falha ao importar: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
