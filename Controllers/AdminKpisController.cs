using ArxReportes.Models;
using ArxReportes.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ArxReportes.Controllers;

[Authorize(Roles = "Admin"), Route("admin/kpis")]
public sealed class AdminKpisController(CatalogStore store) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        ViewBag.Connections = data.Connections.ToDictionary(x => x.Id, x => x.Name);
        return View(data.Kpis.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToList());
    }

    [HttpGet("nuevo")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        var nextOrder = data.Kpis.Count == 0 ? 0 : data.Kpis.Max(x => x.DisplayOrder) + 10;
        return View("Edit", BuildViewModel(new KpiDefinition { DisplayOrder = nextOrder }, data));
    }

    [HttpPost("nuevo"), ValidateAntiForgeryToken]
    public Task<IActionResult> Create(KpiEditViewModel model, CancellationToken cancellationToken) => Save(model, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        var kpi = data.Kpis.FirstOrDefault(x => x.Id == id);
        return kpi is null ? NotFound() : View(BuildViewModel(kpi, data));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(Guid id, KpiEditViewModel model, CancellationToken cancellationToken)
    {
        model.Kpi.Id = id;
        return Save(model, cancellationToken);
    }

    [HttpPost("{id:guid}/eliminar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await store.DeleteKpiAsync(id, cancellationToken);
        TempData["Success"] = "KPI eliminado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ordenar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(List<Guid> ids, CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        var existingIds = data.Kpis.Select(x => x.Id).ToHashSet();
        if (ids.Count != existingIds.Count || ids.Distinct().Count() != ids.Count || ids.Any(x => !existingIds.Contains(x)))
            return BadRequest(new { success = false, message = "El orden recibido no coincide con los KPI actuales. Actualiza la página e inténtalo nuevamente." });

        await store.ReorderKpisAsync(ids, cancellationToken);
        return Json(new { success = true });
    }

    private async Task<IActionResult> Save(KpiEditViewModel model, CancellationToken cancellationToken)
    {
        Normalize(model.Kpi);
        var data = await store.GetAsync(cancellationToken);
        if (!data.Connections.Any(x => x.Id == model.Kpi.ConnectionId))
            ModelState.AddModelError("Kpi.ConnectionId", "Selecciona una conexión válida.");
        if (model.Kpi.LinkedReportId.HasValue && !data.Reports.Any(x => x.Id == model.Kpi.LinkedReportId.Value))
            ModelState.AddModelError("Kpi.LinkedReportId", "Selecciona un reporte relacionado válido.");
        if (!string.IsNullOrWhiteSpace(model.Kpi.Sql) && !IsReadOnlyQuery(model.Kpi.Sql))
            ModelState.AddModelError("Kpi.Sql", "La consulta debe iniciar con SELECT o WITH.");
        if (model.Kpi.Sql.Length > 500_000)
            ModelState.AddModelError("Kpi.Sql", "La consulta es demasiado extensa.");

        if (!ModelState.IsValid)
            return View("Edit", BuildViewModel(model.Kpi, data));

        await store.SaveKpiAsync(model.Kpi, cancellationToken);
        TempData["Success"] = "KPI guardado.";
        return RedirectToAction(nameof(Index));
    }

    private static KpiEditViewModel BuildViewModel(KpiDefinition kpi, CatalogData data) => new()
    {
        Kpi = kpi,
        Connections = data.Connections
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == kpi.ConnectionId))
            .ToList(),
        Reports = data.Reports
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == kpi.LinkedReportId))
            .ToList()
    };

    private static void Normalize(KpiDefinition kpi)
    {
        kpi.Name = (kpi.Name ?? "").Trim();
        kpi.Description = NullIfWhiteSpace(kpi.Description);
        kpi.Sql = (kpi.Sql ?? "").Trim();
        kpi.ValueColumn = (kpi.ValueColumn ?? "").Trim();
        kpi.ComparisonColumn = NullIfWhiteSpace(kpi.ComparisonColumn);
        kpi.DetailColumn = NullIfWhiteSpace(kpi.DetailColumn);
        kpi.ComparisonLabel = NullIfWhiteSpace(kpi.ComparisonLabel);
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsReadOnlyQuery(string sql)
    {
        var normalized = sql.TrimStart().TrimStart('\uFEFF');
        return normalized.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("with", StringComparison.OrdinalIgnoreCase);
    }
}
