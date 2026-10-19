using ArxReportes.Models;
using ArxReportes.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using System.Security.Claims;

namespace ArxReportes.Controllers;

[Route("reportes")]
[Authorize]
public sealed class ReportsController(CatalogStore store, SqlReportRunner runner, ReportKpiCalculator kpiCalculator) : Controller
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Run(Guid id, bool autoRun = false, CancellationToken cancellationToken = default)
    {
        var (report, connection) = await FindActiveAsync(id, cancellationToken);
        if (report is null || connection is null) return NotFound();
        var model = new ReportRunViewModel
        {
            Report = report,
            Values = report.Parameters.ToDictionary(
                x => x.Name,
                x => Request.Query.TryGetValue("p_" + x.Name, out var value) ? value.FirstOrDefault() : x.DefaultValue,
                StringComparer.OrdinalIgnoreCase)
        };
        try
        {
            await PopulateLookupOptionsAsync(model, connection, cancellationToken);
            if (autoRun)
            {
                ValidateLookupSelections(report, model.Values, model.LookupOptions);
                model.Result = await runner.ExecuteAsync(report, connection, model.Values, cancellationToken);
                model.Kpis = kpiCalculator.Calculate(report, model.Result);
                model.Drilldowns = await BuildDrilldownBindingsAsync(report, model.Result, cancellationToken);
            }
        }
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
            model.Kpis = kpiCalculator.Calculate(report, model.Result);
            model.Drilldowns = await BuildDrilldownBindingsAsync(report, model.Result, cancellationToken);
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
            var reportKpis = kpiCalculator.Calculate(report, result);
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Reporte");
            var contentWidth = Math.Max(2, Math.Max(result.Columns.Count, reportKpis.Count));
            var generatedAt = DateTime.Now;
            var nextRow = WriteReportHeader(
                worksheet,
                report,
                connection,
                values,
                lookupOptions,
                User.Identity?.Name,
                generatedAt,
                contentWidth);
            var tableHeaderRow = WriteReportKpis(
                worksheet,
                reportKpis,
                result.Columns.Count,
                result.Truncated,
                nextRow);
            for (var column = 0; column < result.Columns.Count; column++)
            {
                worksheet.Cell(tableHeaderRow, column + 1).Value = result.Columns[column];
            }
            if (result.Columns.Count > 0)
            {
                var headerRange = worksheet.Range(tableHeaderRow, 1, tableHeaderRow, result.Columns.Count);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E654F");
                headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                worksheet.Row(tableHeaderRow).Height = 22;
            }
            var highlightedValues = (report.HighlightFirstColumnValues ?? [])
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var row = 0; row < result.Rows.Count; row++)
            {
                var worksheetRow = tableHeaderRow + row + 1;
                for (var column = 0; column < result.Columns.Count; column++)
                    SetCellValue(worksheet.Cell(worksheetRow, column + 1), result.Rows[row][column]);

                if (IsHighlightedRow(result.Rows[row], highlightedValues) && result.Columns.Count > 0)
                {
                    var highlightedRange = worksheet.Range(worksheetRow, 1, worksheetRow, result.Columns.Count);
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
                var totalsRow = tableHeaderRow + result.Rows.Count + 1;
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
            worksheet.SheetView.FreezeRows(tableHeaderRow);
            if (result.Columns.Count > 0)
                worksheet.Range(tableHeaderRow, 1, tableHeaderRow + result.Rows.Count, result.Columns.Count).SetAutoFilter();
            var lastUsedRow = tableHeaderRow + result.Rows.Count + (report.ShowColumnTotals ? 1 : 0);
            worksheet.ColumnsUsed().AdjustToContents(1, Math.Min(lastUsedRow, tableHeaderRow + 200));
            foreach (var column in worksheet.ColumnsUsed())
                if (column.Width > 38) column.Width = 38;

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

    private async Task<List<ReportDrilldownBindingViewModel>> BuildDrilldownBindingsAsync(
        ReportDefinition sourceReport,
        ReportResult result,
        CancellationToken cancellationToken)
    {
        if (sourceReport.Drilldowns.Count == 0 || result.Columns.Count == 0) return [];

        var data = await store.GetAsync(cancellationToken);
        HashSet<Guid>? allowedReportIds = null;
        if (!User.IsInRole("Admin"))
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return [];
            var user = data.Users.FirstOrDefault(x => x.Id == userId && x.IsActive);
            if (user is null) return [];
            allowedReportIds = user.AllowedReportIds.ToHashSet();
        }

        var bindings = new List<ReportDrilldownBindingViewModel>();
        foreach (var definition in sourceReport.Drilldowns)
        {
            var configuredMappings = GetEffectiveMappings(definition);
            var target = data.Reports.FirstOrDefault(x => x.Id == definition.TargetReportId)
                ?? FindPortableTargetReport(data.Reports, definition.TargetReportName, configuredMappings.FirstOrDefault()?.TargetParameterName);
            if (target is null || !target.IsActive || target.Id == sourceReport.Id) continue;
            if (allowedReportIds is not null && !allowedReportIds.Contains(target.Id)) continue;
            if (!data.Connections.Any(x => x.Id == target.ConnectionId && x.IsActive)) continue;

            var clickableColumnIndex = result.Columns.FindIndex(x =>
                string.Equals(x, definition.SourceColumn, StringComparison.OrdinalIgnoreCase));
            if (clickableColumnIndex < 0) continue;

            var parameterMappings = new List<ReportDrilldownParameterBindingViewModel>();
            var invalidMapping = false;
            foreach (var mapping in configuredMappings)
            {
                var parameter = target.Parameters.FirstOrDefault(x =>
                    string.Equals(x.Name, mapping.TargetParameterName, StringComparison.OrdinalIgnoreCase));
                var sourceColumnIndex = result.Columns.FindIndex(x =>
                    string.Equals(x, mapping.SourceColumn, StringComparison.OrdinalIgnoreCase));
                if (parameter is null || sourceColumnIndex < 0)
                {
                    invalidMapping = true;
                    break;
                }
                parameterMappings.Add(new ReportDrilldownParameterBindingViewModel
                {
                    SourceColumnIndex = sourceColumnIndex,
                    TargetParameterName = parameter.Name,
                    TargetParameterType = parameter.Type,
                    TargetLookupValueType = parameter.LookupValueType,
                    IsRequired = parameter.IsRequired
                });
            }
            if (invalidMapping) continue;

            var sharedParameterNames = definition.PassParentParameters
                ? target.Parameters
                    .Where(targetParameter => sourceReport.Parameters.Any(sourceParameter =>
                        string.Equals(sourceParameter.Name, targetParameter.Name, StringComparison.OrdinalIgnoreCase)))
                    .Select(targetParameter => targetParameter.Name)
                    .ToList()
                : [];
            if (parameterMappings.Count == 0 && sharedParameterNames.Count == 0) continue;

            bindings.Add(new ReportDrilldownBindingViewModel
            {
                ClickableColumnIndex = clickableColumnIndex,
                TargetReportId = target.Id,
                TargetReportName = target.Name,
                ParameterMappings = parameterMappings,
                AutoExecute = definition.AutoExecute,
                OpenInNewTab = definition.OpenInNewTab,
                SharedParameterNames = sharedParameterNames
            });
        }
        return bindings;
    }

    private static ReportDefinition? FindPortableTargetReport(
        IEnumerable<ReportDefinition> reports,
        string? targetName,
        string? targetParameterName)
    {
        if (string.IsNullOrWhiteSpace(targetName)) return null;
        var candidates = string.IsNullOrWhiteSpace(targetParameterName)
            ? reports
            : reports.Where(report => report.Parameters.Any(parameter =>
                string.Equals(parameter.Name, targetParameterName, StringComparison.OrdinalIgnoreCase)));
        return candidates.FirstOrDefault(report => string.Equals(report.Name, targetName, StringComparison.OrdinalIgnoreCase))
            ?? candidates
                .Where(report => report.Name.StartsWith(targetName.Trim() + " ", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(report => report.Name)
                .FirstOrDefault();
    }

    private static List<ReportDrilldownParameterMapping> GetEffectiveMappings(ReportDrilldownDefinition drilldown)
    {
        if (drilldown.ParameterMappings is { Count: > 0 }) return drilldown.ParameterMappings;
        if (string.IsNullOrWhiteSpace(drilldown.TargetParameterName)) return [];
        return
        [
            new ReportDrilldownParameterMapping
            {
                SourceColumn = string.IsNullOrWhiteSpace(drilldown.ValueColumn)
                    ? drilldown.SourceColumn
                    : drilldown.ValueColumn,
                TargetParameterName = drilldown.TargetParameterName
            }
        ];
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

    private static int WriteReportKpis(
        IXLWorksheet worksheet,
        IReadOnlyList<ReportKpiCardViewModel> cards,
        int resultColumnCount,
        bool resultTruncated,
        int startRow)
    {
        if (cards.Count == 0) return startRow;

        var spanColumns = Math.Max(2, Math.Max(cards.Count, resultColumnCount));
        var titleRange = worksheet.Range(startRow, 1, startRow, spanColumns);
        if (spanColumns > 1) titleRange.Merge();
        titleRange.Value = resultTruncated
            ? "INDICADORES DEL REPORTE · RESULTADO LIMITADO"
            : "INDICADORES DEL REPORTE";
        titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#153E34");
        titleRange.Style.Font.FontColor = XLColor.White;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 12;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(startRow).Height = 25;

        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            var column = index + 1;
            var accent = XLColor.FromHtml(GetKpiAccent(card.Definition.Tone));
            var soft = XLColor.FromHtml(GetKpiSoft(card.Definition.Tone));
            var nameCell = worksheet.Cell(startRow + 1, column);
            var valueCell = worksheet.Cell(startRow + 2, column);
            var operationCell = worksheet.Cell(startRow + 3, column);

            nameCell.Value = card.Definition.Name;
            nameCell.Style.Fill.BackgroundColor = accent;
            nameCell.Style.Font.FontColor = XLColor.White;
            nameCell.Style.Font.Bold = true;
            nameCell.Style.Font.FontSize = 10;
            nameCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            nameCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            if (card.NumericValue.HasValue && card.Error is null)
            {
                valueCell.Value = card.NumericValue.Value;
                valueCell.Style.NumberFormat.Format = GetExcelNumberFormat(card.Definition);
            }
            else
            {
                valueCell.Value = card.DisplayValue;
                if (card.Error is not null) valueCell.Style.Font.FontColor = XLColor.FromHtml("#A94C4C");
            }
            valueCell.Style.Fill.BackgroundColor = soft;
            valueCell.Style.Font.Bold = true;
            valueCell.Style.Font.FontSize = 16;
            valueCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            valueCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            operationCell.Value = card.Error ?? card.OperationLabel;
            operationCell.Style.Fill.BackgroundColor = soft;
            operationCell.Style.Font.FontColor = XLColor.FromHtml("#61736B");
            operationCell.Style.Font.FontSize = 9;
            operationCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            operationCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            operationCell.Style.Alignment.WrapText = true;

            var cardRange = worksheet.Range(startRow + 1, column, startRow + 3, column);
            cardRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cardRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#B8CCC2");
        }

        worksheet.Row(startRow + 1).Height = 24;
        worksheet.Row(startRow + 2).Height = 30;
        worksheet.Row(startRow + 3).Height = 28;
        worksheet.Row(startRow + 4).Height = 8;
        return startRow + 5;
    }

    private static int WriteReportHeader(
        IXLWorksheet worksheet,
        ReportDefinition report,
        ConnectionDefinition connection,
        IReadOnlyDictionary<string, string?> values,
        IReadOnlyDictionary<string, List<ReportLookupOption>> lookupOptions,
        string? generatedBy,
        DateTime generatedAt,
        int spanColumns)
    {
        spanColumns = Math.Max(2, spanColumns);
        var companyName = string.IsNullOrWhiteSpace(connection.CompanyName)
            ? connection.Name
            : connection.CompanyName.Trim();

        var companyRange = worksheet.Range(1, 1, 1, spanColumns);
        companyRange.Merge();
        companyRange.Value = companyName;
        companyRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#153E34");
        companyRange.Style.Font.FontColor = XLColor.White;
        companyRange.Style.Font.Bold = true;
        companyRange.Style.Font.FontSize = 18;
        companyRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(1).Height = 34;

        var reportRange = worksheet.Range(2, 1, 2, spanColumns);
        reportRange.Merge();
        reportRange.Value = report.Name;
        reportRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E4F0E9");
        reportRange.Style.Font.FontColor = XLColor.FromHtml("#173F32");
        reportRange.Style.Font.Bold = true;
        reportRange.Style.Font.FontSize = 14;
        reportRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(2).Height = 27;

        var metadataRange = worksheet.Range(3, 1, 3, spanColumns);
        metadataRange.Merge();
        metadataRange.Value = $"Base de datos: {connection.Database}  ·  Generado: {generatedAt:dd/MM/yyyy HH:mm}  ·  Usuario: {(!string.IsNullOrWhiteSpace(generatedBy) ? generatedBy : "No identificado")}";
        metadataRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F4F7F5");
        metadataRange.Style.Font.FontColor = XLColor.FromHtml("#61736B");
        metadataRange.Style.Font.FontSize = 9;
        metadataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(3).Height = 21;
        worksheet.Row(4).Height = 8;

        var filtersTitleRange = worksheet.Range(5, 1, 5, spanColumns);
        filtersTitleRange.Merge();
        filtersTitleRange.Value = "FILTROS APLICADOS";
        filtersTitleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2B6B55");
        filtersTitleRange.Style.Font.FontColor = XLColor.White;
        filtersTitleRange.Style.Font.Bold = true;
        filtersTitleRange.Style.Font.FontSize = 10;
        filtersTitleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(5).Height = 22;

        var row = 6;
        if (report.Parameters.Count == 0)
        {
            var noFiltersRange = worksheet.Range(row, 1, row, spanColumns);
            noFiltersRange.Merge();
            noFiltersRange.Value = "Este reporte no utiliza filtros.";
            noFiltersRange.Style.Font.FontColor = XLColor.FromHtml("#718078");
            noFiltersRange.Style.Font.Italic = true;
            noFiltersRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFBFA");
        }
        else
        {
            foreach (var parameter in report.Parameters)
            {
                values.TryGetValue(parameter.Name, out var rawValue);
                var labelCell = worksheet.Cell(row, 1);
                labelCell.Value = parameter.Label;
                labelCell.Style.Font.Bold = true;
                labelCell.Style.Font.FontColor = XLColor.FromHtml("#31594A");
                labelCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF2ED");

                var valueRange = worksheet.Range(row, 2, row, spanColumns);
                if (spanColumns > 2) valueRange.Merge();
                valueRange.Value = FormatFilterValue(parameter, rawValue, lookupOptions);
                valueRange.Style.Font.FontColor = XLColor.FromHtml("#263B33");
                valueRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAF9");
                valueRange.Style.Alignment.WrapText = true;

                var filterRange = worksheet.Range(row, 1, row, spanColumns);
                filterRange.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                filterRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#CEDBD4");
                worksheet.Row(row).Height = 21;
                row++;
            }
            row--;
        }

        worksheet.Column(1).Width = Math.Max(worksheet.Column(1).Width, 20);
        worksheet.Row(row + 1).Height = 9;
        return row + 2;
    }

    private static string FormatFilterValue(
        ReportParameterDefinition parameter,
        string? rawValue,
        IReadOnlyDictionary<string, List<ReportLookupOption>> lookupOptions)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return "Sin valor";
        if (parameter.Type == ReportParameterType.Consulta &&
            lookupOptions.TryGetValue(parameter.Name, out var options))
        {
            var selected = options.FirstOrDefault(option =>
                string.Equals(option.Value, rawValue, StringComparison.Ordinal));
            if (selected is not null) return selected.Label;
        }
        if (parameter.Type == ReportParameterType.Booleano)
            return rawValue.Equals("true", StringComparison.OrdinalIgnoreCase) || rawValue is "1" or "si" or "sí"
                ? "Sí"
                : "No";
        if (parameter.Type is ReportParameterType.Fecha or ReportParameterType.FechaHora &&
            (DateTime.TryParse(rawValue, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date) ||
             DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)))
            return parameter.Type == ReportParameterType.Fecha
                ? date.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)
                : date.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        return rawValue;
    }

    private static string GetExcelNumberFormat(ReportKpiDefinition definition)
    {
        var decimals = Math.Clamp(definition.DecimalPlaces, 0, 4);
        var decimalPart = decimals == 0 ? "" : "." + new string('0', decimals);
        return definition.Format switch
        {
            ReportKpiValueFormat.Moneda => "\"$\"#,##0" + decimalPart,
            ReportKpiValueFormat.Porcentaje => "#,##0" + decimalPart + "\"%\"",
            _ => "#,##0" + decimalPart
        };
    }

    private static string GetKpiAccent(KpiTone tone) => tone switch
    {
        KpiTone.Azul => "#34729C",
        KpiTone.Ambar => "#A87518",
        KpiTone.Rojo => "#A94C4C",
        KpiTone.Grafito => "#536068",
        _ => "#2B8A58"
    };

    private static string GetKpiSoft(KpiTone tone) => tone switch
    {
        KpiTone.Azul => "#E5F0F6",
        KpiTone.Ambar => "#F7EED9",
        KpiTone.Rojo => "#F7E6E6",
        KpiTone.Grafito => "#E9EDEF",
        _ => "#E5F3EA"
    };

    private static bool IsHighlightedRow(IReadOnlyList<object?> row, ISet<string> configuredValues)
    {
        if (row.Count == 0 || row[0] is null) return false;
        var firstValue = Convert.ToString(row[0], System.Globalization.CultureInfo.CurrentCulture)?.Trim();
        return !string.IsNullOrEmpty(firstValue) && configuredValues.Contains(firstValue);
    }
}
