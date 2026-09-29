using ArxReportes.Models;
using ArxReportes.Services;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ArxReportes.Controllers;

[Authorize(Roles = "Admin"), Route("admin/reportes")]
public sealed class AdminReportsController(CatalogStore store, IWebHostEnvironment environment) : Controller
{
    private const long MaximumImportSize = 1024 * 1024;
    private static readonly JsonSerializerOptions PackageJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        ViewBag.Connections = data.Connections.ToDictionary(x => x.Id, x => x.Name);
        ViewBag.ConnectionItems = data.Connections
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToList();
        return View(data.Reports.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToList());
    }

    [HttpGet("{id:guid}/exportar")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        var report = data.Reports.FirstOrDefault(x => x.Id == id);
        if (report is null) return NotFound();
        var connection = data.Connections.FirstOrDefault(x => x.Id == report.ConnectionId);

        var package = new ReportPackage
        {
            SourceConnectionName = connection?.Name ?? "",
            Report = new PortableReport
            {
                Name = report.Name,
                Description = report.Description,
                Sql = report.Sql,
                CommandTimeoutSeconds = report.CommandTimeoutSeconds,
                DisplayOrder = report.DisplayOrder,
                IsActive = report.IsActive,
                ShowColumnTotals = report.ShowColumnTotals,
                HighlightFirstColumnValues = report.HighlightFirstColumnValues,
                Parameters = report.Parameters.Select(x => new PortableReportParameter
                {
                    Name = x.Name,
                    Label = x.Label,
                    Type = x.Type,
                    IsRequired = x.IsRequired,
                    DefaultValue = x.DefaultValue,
                    Placeholder = x.Placeholder,
                    LookupSql = x.LookupSql,
                    LookupValueColumn = x.LookupValueColumn,
                    LookupDisplayColumns = x.LookupDisplayColumns,
                    LookupValueType = x.LookupValueType
                }).ToList()
            }
        };

        var json = JsonSerializer.Serialize(package, PackageJsonOptions);
        var safeName = MakeSafeFileName(report.Name);
        var fileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.arxreport.json";
        return File(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", fileName);
    }

    [HttpGet("guia-ia-json")]
    public async Task<IActionResult> DownloadAiGuide(CancellationToken cancellationToken)
    {
        var path = Path.Combine(environment.WebRootPath, "examples", "arx-reportes-guia-ia.json");
        if (!System.IO.File.Exists(path)) return NotFound();
        var content = await System.IO.File.ReadAllBytesAsync(path, cancellationToken);
        return File(content, "application/json; charset=utf-8", $"ARX_Reportes_Guia_IA_{DateTime.Now:yyyyMMdd_HHmmss}.json");
    }

    [HttpPost("importar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file, string? jsonText, Guid? connectionId, CancellationToken cancellationToken)
    {
        var hasFile = file is not null && file.Length > 0;
        var hasText = !string.IsNullOrWhiteSpace(jsonText);
        if (!hasFile && !hasText)
        {
            TempData["Error"] = "Selecciona un archivo JSON o pega el contenido del reporte.";
            return RedirectToAction(nameof(Index));
        }
        if (hasFile && file!.Length > MaximumImportSize)
        {
            TempData["Error"] = "El archivo supera el máximo permitido de 1 MB.";
            return RedirectToAction(nameof(Index));
        }
        if (!hasFile && Encoding.UTF8.GetByteCount(jsonText!) > MaximumImportSize)
        {
            TempData["Error"] = "El texto JSON supera el máximo permitido de 1 MB.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            ReportPackage? package;
            if (hasFile)
            {
                await using var stream = file!.OpenReadStream();
                package = await JsonSerializer.DeserializeAsync<ReportPackage>(stream, PackageJsonOptions, cancellationToken);
            }
            else
            {
                package = JsonSerializer.Deserialize<ReportPackage>(jsonText!, PackageJsonOptions);
            }

            if (package is null || package.SchemaVersion != 1)
                throw new InvalidOperationException("El archivo no corresponde a una versión compatible de Arx Reportes.");

            ValidatePackage(package);
            var data = await store.GetAsync(cancellationToken);
            var destination = connectionId.HasValue
                ? data.Connections.FirstOrDefault(x => x.Id == connectionId.Value && x.IsActive)
                : data.Connections.FirstOrDefault(x => x.IsActive && string.Equals(x.Name, package.SourceConnectionName, StringComparison.OrdinalIgnoreCase));

            if (destination is null)
                throw new InvalidOperationException("No se encontró la conexión de origen. Vuelve a importar y selecciona una conexión destino.");

            var importedAt = DateTime.Now;
            var report = new ReportDefinition
            {
                Id = Guid.NewGuid(),
                Name = $"{package.Report.Name} {importedAt:yyyy-MM-dd HH-mm-ss}",
                Description = package.Report.Description,
                ConnectionId = destination.Id,
                Sql = package.Report.Sql,
                CommandTimeoutSeconds = package.Report.CommandTimeoutSeconds,
                DisplayOrder = package.Report.DisplayOrder,
                IsActive = package.Report.IsActive,
                ShowColumnTotals = package.Report.ShowColumnTotals,
                HighlightFirstColumnValues = NormalizeHighlightValues(package.Report.HighlightFirstColumnValues),
                Parameters = package.Report.Parameters.Select(x => new ReportParameterDefinition
                {
                    Name = x.Name,
                    Label = x.Label,
                    Type = x.Type,
                    IsRequired = x.IsRequired,
                    DefaultValue = x.DefaultValue,
                    Placeholder = x.Placeholder,
                    LookupSql = x.LookupSql,
                    LookupValueColumn = x.LookupValueColumn,
                    LookupDisplayColumns = x.LookupDisplayColumns,
                    LookupValueType = x.LookupValueType
                }).ToList()
            };

            await store.SaveReportAsync(report, cancellationToken);
            TempData["Success"] = $"Reporte importado como '{report.Name}'.";
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            TempData["Error"] = "No fue posible importar el reporte: " + exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("nuevo")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Edit", await BuildViewModel(new ReportDefinition(), cancellationToken));

    [HttpPost("nuevo"), ValidateAntiForgeryToken]
    public Task<IActionResult> Create(ReportEditViewModel model, CancellationToken cancellationToken) => Save(model, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var report = (await store.GetAsync(cancellationToken)).Reports.FirstOrDefault(x => x.Id == id);
        return report is null ? NotFound() : View(await BuildViewModel(report, cancellationToken));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(Guid id, ReportEditViewModel model, CancellationToken cancellationToken)
    {
        model.Report.Id = id;
        return Save(model, cancellationToken);
    }

    [HttpPost("{id:guid}/eliminar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await store.DeleteReportAsync(id, cancellationToken);
        TempData["Success"] = "Reporte eliminado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ordenar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(List<Guid> ids, CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        var existingIds = data.Reports.Select(x => x.Id).ToHashSet();
        if (ids.Count != existingIds.Count || ids.Distinct().Count() != ids.Count || ids.Any(x => !existingIds.Contains(x)))
            return BadRequest(new { success = false, message = "El orden recibido no coincide con los reportes actuales. Actualiza la página e inténtalo nuevamente." });

        await store.ReorderReportsAsync(ids, cancellationToken);
        return Json(new { success = true });
    }

    private async Task<IActionResult> Save(ReportEditViewModel model, CancellationToken cancellationToken)
    {
        model.Report.HighlightFirstColumnValues = ParseHighlightValues(model.HighlightFirstColumnValuesText);
        model.Report.Parameters = model.Report.Parameters
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.Label))
            .ToList();

        var data = await store.GetAsync(cancellationToken);
        if (!data.Connections.Any(x => x.Id == model.Report.ConnectionId))
            ModelState.AddModelError("Report.ConnectionId", "Seleccione una conexión válida.");
        if (model.Report.Parameters.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            ModelState.AddModelError("Report.Parameters", "Los nombres de parámetros no pueden repetirse.");
        if (model.Report.HighlightFirstColumnValues.Count > 200)
            ModelState.AddModelError(nameof(model.HighlightFirstColumnValuesText), "Puedes configurar un máximo de 200 valores para destacar.");
        if (model.Report.HighlightFirstColumnValues.Any(x => x.Length > 250))
            ModelState.AddModelError(nameof(model.HighlightFirstColumnValuesText), "Cada valor para destacar puede tener como máximo 250 caracteres.");
        for (var index = 0; index < model.Report.Parameters.Count; index++)
        {
            var parameter = model.Report.Parameters[index];
            if (parameter.Type != ReportParameterType.Consulta) continue;
            if (string.IsNullOrWhiteSpace(parameter.LookupSql))
                ModelState.AddModelError($"Report.Parameters[{index}].LookupSql", "Indica la consulta que llenará la lista.");
            else if (!IsReadOnlyQuery(parameter.LookupSql))
                ModelState.AddModelError($"Report.Parameters[{index}].LookupSql", "La consulta de opciones debe iniciar con SELECT o WITH.");
            if (string.IsNullOrWhiteSpace(parameter.LookupValueColumn))
                ModelState.AddModelError($"Report.Parameters[{index}].LookupValueColumn", "Indica la columna que se enviará como valor.");
        }

        if (!ModelState.IsValid)
        {
            model.Connections = BuildConnectionItems(data, model.Report.ConnectionId);
            return View("Edit", model);
        }

        await store.SaveReportAsync(model.Report, cancellationToken);
        TempData["Success"] = "Reporte guardado.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ReportEditViewModel> BuildViewModel(ReportDefinition report, CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        return new ReportEditViewModel
        {
            Report = report,
            Connections = BuildConnectionItems(data, report.ConnectionId),
            HighlightFirstColumnValuesText = string.Join(Environment.NewLine, report.HighlightFirstColumnValues ?? [])
        };
    }

    private static List<SelectListItem> BuildConnectionItems(CatalogData data, Guid selectedId) => data.Connections
        .OrderBy(x => x.Name)
        .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == selectedId))
        .ToList();

    private static void ValidatePackage(ReportPackage package)
    {
        var report = package.Report;
        if (string.IsNullOrWhiteSpace(report.Name) || report.Name.Length > 120)
            throw new InvalidOperationException("El nombre del reporte no es válido.");
        if (report.Description?.Length > 500)
            throw new InvalidOperationException("La descripción supera el máximo permitido.");
        if (string.IsNullOrWhiteSpace(report.Sql) || report.Sql.Length > 500_000)
            throw new InvalidOperationException("La sentencia SQL no es válida.");
        if (report.CommandTimeoutSeconds is < 1 or > 600 || report.DisplayOrder is < 0 or > 9999)
            throw new InvalidOperationException("La configuración de ejecución no es válida.");
        if (report.Parameters.Count > 100)
            throw new InvalidOperationException("El reporte contiene demasiados parámetros.");
        report.HighlightFirstColumnValues = NormalizeHighlightValues(report.HighlightFirstColumnValues);
        if (report.HighlightFirstColumnValues.Count > 200)
            throw new InvalidOperationException("El reporte contiene más de 200 valores para destacar.");
        if (report.HighlightFirstColumnValues.Any(x => x.Length > 250))
            throw new InvalidOperationException("Uno de los valores para destacar supera los 250 caracteres.");
        if (report.Parameters.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new InvalidOperationException("El archivo contiene parámetros repetidos.");

        foreach (var parameter in report.Parameters)
        {
            var target = new ReportParameterDefinition
            {
                Name = parameter.Name,
                Label = parameter.Label,
                Type = parameter.Type,
                IsRequired = parameter.IsRequired,
                DefaultValue = parameter.DefaultValue,
                Placeholder = parameter.Placeholder,
                LookupSql = parameter.LookupSql,
                LookupValueColumn = parameter.LookupValueColumn,
                LookupDisplayColumns = parameter.LookupDisplayColumns,
                LookupValueType = parameter.LookupValueType
            };
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(target, new ValidationContext(target), results, true))
                throw new InvalidOperationException(results.First().ErrorMessage ?? "Uno de los parámetros no es válido.");
            if (parameter.Type == ReportParameterType.Consulta &&
                (string.IsNullOrWhiteSpace(parameter.LookupSql) ||
                 string.IsNullOrWhiteSpace(parameter.LookupValueColumn) ||
                 !IsReadOnlyQuery(parameter.LookupSql)))
                throw new InvalidOperationException($"El catálogo '{parameter.Label}' no tiene una consulta válida o una columna de valor.");
        }
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "reporte" : cleaned;
    }

    private static List<string> ParseHighlightValues(string? value) => NormalizeHighlightValues(
        (value ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

    private static List<string> NormalizeHighlightValues(IEnumerable<string>? values) => (values ?? [])
        .Select(x => x?.Trim() ?? "")
        .Where(x => x.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static bool IsReadOnlyQuery(string sql)
    {
        var normalized = sql.TrimStart().TrimStart('\uFEFF');
        return normalized.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("with", StringComparison.OrdinalIgnoreCase);
    }
}
