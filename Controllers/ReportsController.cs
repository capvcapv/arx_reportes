using ArxReportes.Models;
using ArxReportes.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ArxReportes.Controllers;

[Route("reportes")]
[Authorize]
public sealed class ReportsController(CatalogStore store, SqlReportRunner runner) : Controller
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Run(Guid id, CancellationToken cancellationToken)
    {
        var (report, connection) = await FindActiveAsync(id, cancellationToken);
        if (report is null || connection is null) return NotFound();
        var model = new ReportRunViewModel
        {
            Report = report,
            Values = report.Parameters.ToDictionary(x => x.Name, x => x.DefaultValue, StringComparer.OrdinalIgnoreCase)
        };
        try { await PopulateLookupOptionsAsync(model, connection, cancellationToken); }
        catch (Exception exception) { model.Error = FriendlyMessage(exception); }
        return View(model);
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Execute(Guid id, CancellationToken cancellationToken)
    {
        var (report, connection) = await FindActiveAsync(id, cancellationToken);
        if (report is null || connection is null) return NotFound();
        var values = ReadValues(report);
        var model = new ReportRunViewModel { Report = report, Values = values };
        try
        {
            await PopulateLookupOptionsAsync(model, connection, cancellationToken);
            ValidateLookupSelections(report, values, model.LookupOptions);
            model.Result = await runner.ExecuteAsync(report, connection, values, cancellationToken);
        }
        catch (Exception exception) { model.Error = FriendlyMessage(exception); }
        return View("Run", model);
    }

    [HttpPost("{id:guid}/excel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var (report, connection) = await FindActiveAsync(id, cancellationToken);
        if (report is null || connection is null) return NotFound();
        var values = ReadValues(report);
        try
        {
            var lookupOptions = new Dictionary<string, List<ReportLookupOption>>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in report.Parameters.Where(x => x.Type == ReportParameterType.Consulta))
                lookupOptions[parameter.Name] = await runner.GetLookupOptionsAsync(parameter, connection, cancellationToken);
            ValidateLookupSelections(report, values, lookupOptions);
            var result = await runner.ExecuteAsync(report, connection, values, cancellationToken);
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Reporte");
            for (var column = 0; column < result.Columns.Count; column++)
            {
                worksheet.Cell(1, column + 1).Value = result.Columns[column];
                worksheet.Cell(1, column + 1).Style.Font.Bold = true;
            }
            var highlightedValues = (report.HighlightFirstColumnValues ?? [])
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var row = 0; row < result.Rows.Count; row++)
            {
                for (var column = 0; column < result.Columns.Count; column++)
                    SetCellValue(worksheet.Cell(row + 2, column + 1), result.Rows[row][column]);

                if (IsHighlightedRow(result.Rows[row], highlightedValues) && result.Columns.Count > 0)
                {
                    var highlightedRange = worksheet.Range(row + 2, 1, row + 2, result.Columns.Count);
                    highlightedRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF1B8");
                    highlightedRange.Style.Font.Bold = true;
                    highlightedRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    highlightedRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    highlightedRange.Style.Border.TopBorderColor = XLColor.FromHtml("#D5A72D");
                    highlightedRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#D5A72D");
                }
            }

            if (report.ShowColumnTotals && result.ColumnTotals.Any(x => x.HasValue))
            {
                var totalsRow = result.Rows.Count + 2;
                var labelColumn = result.ColumnTotals.FindIndex(x => !x.HasValue);
                for (var column = 0; column < result.Columns.Count; column++)
                {
                    var total = result.ColumnTotals[column];
                    if (total.HasValue)
                        worksheet.Cell(totalsRow, column + 1).Value = total.Value;
                    else if (column == labelColumn)
                        worksheet.Cell(totalsRow, column + 1).Value = "Totales";
                }
                var totalsRange = worksheet.Range(totalsRow, 1, totalsRow, result.Columns.Count);
                totalsRange.Style.Font.Bold = true;
                totalsRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCEBE4");
                totalsRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }
            worksheet.SheetView.FreezeRows(1);
            if (result.Columns.Count > 0)
                worksheet.Range(1, 1, result.Rows.Count + 1, result.Columns.Count).SetAutoFilter();
            worksheet.ColumnsUsed().AdjustToContents(1, Math.Min(result.Rows.Count + 1, 200));

            await using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var safeName = string.Join("_", report.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{safeName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }
        catch (Exception exception)
        {
            TempData["Error"] = FriendlyMessage(exception);
            return RedirectToAction(nameof(Run), new { id });
        }
    }

    private async Task<(ReportDefinition?, ConnectionDefinition?)> FindActiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var data = await store.GetAsync(cancellationToken);
        var report = data.Reports.FirstOrDefault(x => x.Id == id && x.IsActive);
        if (report is not null && !User.IsInRole("Admin"))
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                report = null;
            else
            {
                var user = data.Users.FirstOrDefault(x => x.Id == userId && x.IsActive);
                if (user is null || !user.AllowedReportIds.Contains(id)) report = null;
            }
        }
        var connection = report is null ? null : data.Connections.FirstOrDefault(x => x.Id == report.ConnectionId && x.IsActive);
        return (report, connection);
    }

    private Dictionary<string, string?> ReadValues(ReportDefinition report) => report.Parameters.ToDictionary(
        x => x.Name,
        x => Request.Form.TryGetValue(x.Name, out var value) ? value.FirstOrDefault() : null,
        StringComparer.OrdinalIgnoreCase);

    private async Task PopulateLookupOptionsAsync(ReportRunViewModel model, ConnectionDefinition connection, CancellationToken cancellationToken)
    {
        foreach (var parameter in model.Report.Parameters.Where(x => x.Type == ReportParameterType.Consulta))
            model.LookupOptions[parameter.Name] = await runner.GetLookupOptionsAsync(parameter, connection, cancellationToken);
    }

    private static void ValidateLookupSelections(
        ReportDefinition report,
        IReadOnlyDictionary<string, string?> values,
        IReadOnlyDictionary<string, List<ReportLookupOption>> lookupOptions)
    {
        foreach (var parameter in report.Parameters.Where(x => x.Type == ReportParameterType.Consulta))
        {
            values.TryGetValue(parameter.Name, out var selectedValue);
            if (string.IsNullOrWhiteSpace(selectedValue)) continue;
            if (!lookupOptions.TryGetValue(parameter.Name, out var options) ||
                !options.Any(x => string.Equals(x.Value, selectedValue, StringComparison.Ordinal)))
                throw new InvalidOperationException($"La opción seleccionada para '{parameter.Label}' ya no está disponible.");
        }
    }

    private static string FriendlyMessage(Exception exception) => exception switch
    {
        InvalidOperationException => exception.Message,
        Microsoft.Data.SqlClient.SqlException => "SQL Server rechazó la consulta: " + exception.Message,
        _ => "No fue posible ejecutar el reporte: " + exception.Message
    };

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: cell.Clear(); break;
            case DateTime date: cell.Value = date; cell.Style.DateFormat.Format = "dd/mm/yyyy hh:mm"; break;
            case bool boolean: cell.Value = boolean; break;
            case byte number: cell.Value = number; break;
            case short number: cell.Value = number; break;
            case int number: cell.Value = number; break;
            case long number: cell.Value = number; break;
            case float number: cell.Value = number; break;
            case double number: cell.Value = number; break;
            case decimal number: cell.Value = number; break;
            default: cell.Value = Convert.ToString(value) ?? ""; break;
        }
    }

    private static bool IsHighlightedRow(IReadOnlyList<object?> row, ISet<string> configuredValues)
    {
        if (row.Count == 0 || row[0] is null) return false;
        var firstValue = Convert.ToString(row[0], System.Globalization.CultureInfo.CurrentCulture)?.Trim();
        return !string.IsNullOrEmpty(firstValue) && configuredValues.Contains(firstValue);
    }
}
