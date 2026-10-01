using System.Globalization;
using ArxReportes.Models;

namespace ArxReportes.Services;

public sealed class ReportKpiCalculator
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("es-MX");

    public List<ReportKpiCardViewModel> Calculate(ReportDefinition report, ReportResult result) =>
        (report.ReportKpis ?? []).Select(definition => CalculateOne(definition, result)).ToList();

    private static ReportKpiCardViewModel CalculateOne(ReportKpiDefinition definition, ReportResult result)
    {
        var card = new ReportKpiCardViewModel
        {
            Definition = definition,
            OperationLabel = BuildOperationLabel(definition)
        };

        try
        {
            int? columnIndex = null;
            if (!string.IsNullOrWhiteSpace(definition.ColumnName))
            {
                var index = result.Columns.FindIndex(column =>
                    string.Equals(column, definition.ColumnName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                    throw new InvalidOperationException($"No existe la columna '{definition.ColumnName}'.");
                columnIndex = index;
            }

            if (definition.Operation != ReportKpiOperation.Conteo && !columnIndex.HasValue)
                throw new InvalidOperationException("Este cálculo necesita una columna.");

            decimal? value = definition.Operation switch
            {
                ReportKpiOperation.Conteo => columnIndex.HasValue
                    ? result.Rows.Count(row => row[columnIndex.Value] is not null)
                    : result.Rows.Count,
                ReportKpiOperation.ConteoDistinto => CountDistinct(result, columnIndex!.Value),
                ReportKpiOperation.Suma => GetNumbers(result, columnIndex!.Value, definition.ColumnName!).Sum(),
                ReportKpiOperation.Promedio => Average(GetNumbers(result, columnIndex!.Value, definition.ColumnName!)),
                ReportKpiOperation.Minimo => Minimum(GetNumbers(result, columnIndex!.Value, definition.ColumnName!)),
                ReportKpiOperation.Maximo => Maximum(GetNumbers(result, columnIndex!.Value, definition.ColumnName!)),
                _ => null
            };

            card.DisplayValue = value.HasValue
                ? FormatValue(value.Value, definition.Format, definition.DecimalPlaces)
                : "Sin datos";
            card.NumericValue = value;
        }
        catch (Exception exception) when (exception is InvalidOperationException or OverflowException)
        {
            card.Error = exception.Message;
            card.DisplayValue = "No disponible";
        }

        return card;
    }

    private static List<decimal> GetNumbers(ReportResult result, int columnIndex, string columnName)
    {
        var numbers = new List<decimal>();
        foreach (var row in result.Rows)
        {
            var rawValue = row[columnIndex];
            if (rawValue is null) continue;
            try { numbers.Add(Convert.ToDecimal(rawValue, CultureInfo.InvariantCulture)); }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                throw new InvalidOperationException($"La columna '{columnName}' contiene valores no numéricos.");
            }
        }
        return numbers;
    }

    private static decimal CountDistinct(ReportResult result, int columnIndex)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in result.Rows)
        {
            var value = row[columnIndex];
            if (value is null) continue;
            values.Add(value switch
            {
                DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "",
                _ => value.ToString() ?? ""
            });
        }
        return values.Count;
    }

    private static decimal? Average(IReadOnlyCollection<decimal> values) => values.Count == 0 ? null : values.Average();
    private static decimal? Minimum(IReadOnlyCollection<decimal> values) => values.Count == 0 ? null : values.Min();
    private static decimal? Maximum(IReadOnlyCollection<decimal> values) => values.Count == 0 ? null : values.Max();

    private static string FormatValue(decimal value, ReportKpiValueFormat format, int decimalPlaces)
    {
        var decimals = Math.Clamp(decimalPlaces, 0, 4);
        return format switch
        {
            ReportKpiValueFormat.Moneda => value.ToString($"C{decimals}", DisplayCulture),
            ReportKpiValueFormat.Porcentaje => value.ToString($"N{decimals}", DisplayCulture) + "%",
            _ => value.ToString($"N{decimals}", DisplayCulture)
        };
    }

    private static string BuildOperationLabel(ReportKpiDefinition definition)
    {
        var column = string.IsNullOrWhiteSpace(definition.ColumnName) ? "filas" : definition.ColumnName;
        return definition.Operation switch
        {
            ReportKpiOperation.Promedio => $"Promedio de {column}",
            ReportKpiOperation.Conteo => string.IsNullOrWhiteSpace(definition.ColumnName) ? "Conteo de filas" : $"Conteo de {column}",
            ReportKpiOperation.ConteoDistinto => $"Valores distintos de {column}",
            ReportKpiOperation.Minimo => $"Mínimo de {column}",
            ReportKpiOperation.Maximo => $"Máximo de {column}",
            _ => $"Suma de {column}"
        };
    }
}
