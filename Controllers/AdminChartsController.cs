using ArxReportes.Models;
using ArxReportes.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ArxReportes.Controllers;

[Authorize(Roles = "Admin"), Route("admin/graficas")]
public sealed class AdminChartsController(CatalogStore store) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        ViewBag.Connections = data.Connections.ToDictionary(x => x.Id, x => x.Name);
        return View(data.Charts.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToList());
    }

    [HttpGet("nueva")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        var nextOrder = data.Charts.Count == 0 ? 0 : data.Charts.Max(x => x.DisplayOrder) + 10;
        return View("Edit", BuildViewModel(new ChartDefinition { DisplayOrder = nextOrder }, data));
    }

    [HttpPost("nueva"), ValidateAntiForgeryToken]
    public Task<IActionResult> Create(ChartEditViewModel model, CancellationToken cancellationToken) => Save(model, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        var chart = data.Charts.FirstOrDefault(x => x.Id == id);
        return chart is null ? NotFound() : View(BuildViewModel(chart, data));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(Guid id, ChartEditViewModel model, CancellationToken cancellationToken)
    {
        model.Chart.Id = id;
        return Save(model, cancellationToken);
    }

    [HttpPost("{id:guid}/eliminar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await store.DeleteChartAsync(id, cancellationToken);
        TempData["Success"] = "Gráfica eliminada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ordenar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(List<Guid> ids, CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        var existingIds = data.Charts.Select(x => x.Id).ToHashSet();
        if (ids.Count != existingIds.Count || ids.Distinct().Count() != ids.Count || ids.Any(x => !existingIds.Contains(x)))
            return BadRequest(new { success = false, message = "El orden recibido no coincide con las gráficas actuales. Actualiza la página e inténtalo nuevamente." });

        await store.ReorderChartsAsync(ids, cancellationToken);
        return Json(new { success = true });
    }

    private async Task<IActionResult> Save(ChartEditViewModel model, CancellationToken cancellationToken)
    {
        Normalize(model.Chart);
        var data = await store.GetAsync(cancellationToken);
        if (!data.Connections.Any(x => x.Id == model.Chart.ConnectionId))
            ModelState.AddModelError("Chart.ConnectionId", "Selecciona una conexión válida.");
        if (model.Chart.LinkedReportId.HasValue && !data.Reports.Any(x => x.Id == model.Chart.LinkedReportId.Value))
            ModelState.AddModelError("Chart.LinkedReportId", "Selecciona un reporte relacionado válido.");
        if (!string.IsNullOrWhiteSpace(model.Chart.Sql) && !IsReadOnlyQuery(model.Chart.Sql))
            ModelState.AddModelError("Chart.Sql", "La consulta debe iniciar con SELECT o WITH.");
        if (model.Chart.Sql.Length > 500_000)
            ModelState.AddModelError("Chart.Sql", "La consulta es demasiado extensa.");

        if (!ModelState.IsValid)
            return View("Edit", BuildViewModel(model.Chart, data));

        await store.SaveChartAsync(model.Chart, cancellationToken);
        TempData["Success"] = "Gráfica guardada.";
        return RedirectToAction(nameof(Index));
    }

    private static ChartEditViewModel BuildViewModel(ChartDefinition chart, CatalogData data) => new()
    {
        Chart = chart,
        Connections = data.Connections
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == chart.ConnectionId))
            .ToList(),
        Reports = data.Reports
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == chart.LinkedReportId))
            .ToList()
    };

    private static void Normalize(ChartDefinition chart)
    {
        chart.Name = (chart.Name ?? "").Trim();
        chart.Description = NullIfWhiteSpace(chart.Description);
        chart.Sql = (chart.Sql ?? "").Trim();
        chart.LabelColumn = (chart.LabelColumn ?? "").Trim();
        chart.ValueColumn = (chart.ValueColumn ?? "").Trim();
        chart.SeriesColumn = NullIfWhiteSpace(chart.SeriesColumn);
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsReadOnlyQuery(string sql)
    {
        var normalized = sql.TrimStart().TrimStart('\uFEFF');
        return normalized.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("with", StringComparison.OrdinalIgnoreCase);
    }
}
