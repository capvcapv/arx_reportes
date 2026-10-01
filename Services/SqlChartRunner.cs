using System.Data;
using System.Globalization;
using ArxReportes.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace ArxReportes.Services;

public sealed class SqlChartRunner(
    CatalogStore store,
    IMemoryCache cache,
    IConfiguration configuration,
    ILogger<SqlChartRunner> logger)
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("es-MX");

    public async Task<ChartCardViewModel> GetAsync(
        ChartDefinition chart,
        ConnectionDefinition connection,
        bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ChartQueryResult result;
            var cacheKey = $"arx-chart:{chart.Id:N}:{chart.CacheVersion:N}";
            if (!forceRefresh && chart.CacheMinutes > 0 &&
                cache.TryGetValue(cacheKey, out ChartQueryResult? cached) && cached is not null)
            {
                result = cached;
            }
            else
            {
                result = await ExecuteAsync(chart, connection, cancellationToken);
                if (chart.CacheMinutes > 0)
                    cache.Set(cacheKey, result, TimeSpan.FromMinutes(chart.CacheMinutes));
            }

            return new ChartCardViewModel
            {
                Definition = chart,
                Labels = result.Labels,
                DataSets = result.DataSets,
                UpdatedAtUtc = result.UpdatedAtUtc,
                Truncated = result.Truncated
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "No fue posible actualizar la gráfica {ChartId} ({ChartName}).", chart.Id, chart.Name);
            return new ChartCardViewModel
            {
                Definition = chart,
                Error = "No se pudo actualizar la gráfica.",
                UpdatedAtUtc = DateTime.UtcNow
            };
        }
    }

    private async Task<ChartQueryResult> ExecuteAsync(
        ChartDefinition chart,
        ConnectionDefinition connection,
        CancellationToken cancellationToken)
    {
        ValidateReadOnlyQuery(chart.Sql);
        var maximumRows = Math.Clamp(configuration.GetValue("Charts:MaximumRows", 500), 1, 5000);
        var maximumSeries = Math.Clamp(configuration.GetValue("Charts:MaximumSeries", 12), 1, 30);

        await using var sqlConnection = new SqlConnection(BuildConnectionString(connection));
        await sqlConnection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(chart.Sql, sqlConnection)
        {
            CommandTimeout = chart.CommandTimeoutSeconds,
            CommandType = CommandType.Text
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        int labelOrdinal;
        int valueOrdinal;
        int? seriesOrdinal = null;
        try
        {
            labelOrdinal = reader.GetOrdinal(chart.LabelColumn);
            valueOrdinal = reader.GetOrdinal(chart.ValueColumn);
            if (!string.IsNullOrWhiteSpace(chart.SeriesColumn))
                seriesOrdinal = reader.GetOrdinal(chart.SeriesColumn);
        }
        catch (IndexOutOfRangeException)
        {
            throw new InvalidOperationException("Alguna columna configurada para la gráfica no existe en el resultado.");
        }

        var labels = new List<string>();
        var labelSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seriesOrder = new List<string>();
        var valuesBySeries = new Dictionary<string, Dictionary<string, decimal?>>(StringComparer.OrdinalIgnoreCase);
        var rowCount = 0;
        var truncated = false;

        while (await reader.ReadAsync(cancellationToken))
        {
            if (rowCount >= maximumRows)
            {
                truncated = true;
                break;
            }
            rowCount++;

            var label = await reader.IsDBNullAsync(labelOrdinal, cancellationToken)
                ? "(Sin etiqueta)"
                : Convert.ToString(reader.GetValue(labelOrdinal), DisplayCulture)?.Trim();
            if (string.IsNullOrWhiteSpace(label)) label = "(Sin etiqueta)";

            var series = "Valor";
            if (seriesOrdinal.HasValue && !await reader.IsDBNullAsync(seriesOrdinal.Value, cancellationToken))
                series = Convert.ToString(reader.GetValue(seriesOrdinal.Value), DisplayCulture)?.Trim() ?? "Sin serie";
            if (string.IsNullOrWhiteSpace(series)) series = "Sin serie";

            if (!valuesBySeries.TryGetValue(series, out var seriesValues))
            {
                if (valuesBySeries.Count >= maximumSeries)
                    throw new InvalidOperationException($"La consulta devolvió más de {maximumSeries} series. Agrupa o filtra los datos.");
                seriesValues = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
                valuesBySeries.Add(series, seriesValues);
                seriesOrder.Add(series);
            }

            if (labelSet.Add(label)) labels.Add(label);
            decimal? value = await reader.IsDBNullAsync(valueOrdinal, cancellationToken)
                ? null
                : ConvertToDecimal(reader.GetValue(valueOrdinal), chart.ValueColumn);

            if (seriesValues.TryGetValue(label, out var existing) && existing.HasValue && value.HasValue)
                seriesValues[label] = existing.Value + value.Value;
            else if (!seriesValues.ContainsKey(label) || value.HasValue)
                seriesValues[label] = value;
        }

        if (rowCount == 0)
            throw new InvalidOperationException("La consulta de la gráfica no devolvió filas.");

        var dataSets = seriesOrder.Select(series => new ChartDataSetViewModel
        {
            Name = series,
            Values = labels.Select(label => valuesBySeries[series].GetValueOrDefault(label)).ToList()
        }).ToList();

        return new ChartQueryResult(labels, dataSets, truncated, DateTime.UtcNow);
    }

    private string BuildConnectionString(ConnectionDefinition connection)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = connection.Server,
            InitialCatalog = connection.Database,
            IntegratedSecurity = connection.IntegratedSecurity,
            Encrypt = connection.Encrypt,
            TrustServerCertificate = connection.TrustServerCertificate,
            ConnectTimeout = 15,
            ApplicationName = "Arx Reportes Graficas"
        };
        if (!connection.IntegratedSecurity)
        {
            builder.UserID = connection.UserName;
            builder.Password = connection.Password ?? store.UnprotectPassword(connection);
        }
        return builder.ConnectionString;
    }

    private static decimal ConvertToDecimal(object value, string column)
    {
        try { return Convert.ToDecimal(value, CultureInfo.InvariantCulture); }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new InvalidOperationException($"La columna '{column}' debe devolver un valor numérico.");
        }
    }

    private static void ValidateReadOnlyQuery(string sql)
    {
        var normalized = sql.TrimStart().TrimStart('\uFEFF');
        if (!(normalized.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
              normalized.StartsWith("with", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Las gráficas sólo admiten consultas SELECT o CTE (WITH).");
    }

    private sealed record ChartQueryResult(
        List<string> Labels,
        List<ChartDataSetViewModel> DataSets,
        bool Truncated,
        DateTime UpdatedAtUtc);
}
