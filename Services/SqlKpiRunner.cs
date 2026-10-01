using System.Data;
using System.Globalization;
using ArxReportes.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace ArxReportes.Services;

public sealed class SqlKpiRunner(
    CatalogStore store,
    IMemoryCache cache,
    ILogger<SqlKpiRunner> logger)
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("es-MX");

    public async Task<KpiCardViewModel> GetAsync(
        KpiDefinition kpi,
        ConnectionDefinition connection,
        bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        try
        {
            KpiQueryResult result;
            var cacheKey = $"arx-kpi:{kpi.Id:N}:{kpi.CacheVersion:N}";
            if (!forceRefresh && kpi.CacheMinutes > 0 && cache.TryGetValue(cacheKey, out KpiQueryResult? cached) && cached is not null)
            {
                result = cached;
            }
            else
            {
                result = await ExecuteAsync(kpi, connection, cancellationToken);
                if (kpi.CacheMinutes > 0)
                    cache.Set(cacheKey, result, TimeSpan.FromMinutes(kpi.CacheMinutes));
            }

            return new KpiCardViewModel
            {
                Definition = kpi,
                DisplayValue = FormatValue(result.Value, result.NumericValue, kpi),
                Detail = result.Detail,
                ChangePercent = result.ChangePercent,
                IsPositive = result.ChangePercent.HasValue
                    ? kpi.LowerIsBetter ? result.ChangePercent <= 0 : result.ChangePercent >= 0
                    : null,
                UpdatedAtUtc = result.UpdatedAtUtc
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "No fue posible actualizar el KPI {KpiId} ({KpiName}).", kpi.Id, kpi.Name);
            return new KpiCardViewModel
            {
                Definition = kpi,
                Error = "No se pudo actualizar",
                UpdatedAtUtc = DateTime.UtcNow
            };
        }
    }

    private async Task<KpiQueryResult> ExecuteAsync(
        KpiDefinition kpi,
        ConnectionDefinition connection,
        CancellationToken cancellationToken)
    {
        ValidateReadOnlyQuery(kpi.Sql);
        await using var sqlConnection = new SqlConnection(BuildConnectionString(connection));
        await sqlConnection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(kpi.Sql, sqlConnection)
        {
            CommandTimeout = kpi.CommandTimeoutSeconds,
            CommandType = CommandType.Text
        };
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("La consulta del KPI no devolvió filas.");

        int valueOrdinal;
        int? comparisonOrdinal = null;
        int? detailOrdinal = null;
        try
        {
            valueOrdinal = reader.GetOrdinal(kpi.ValueColumn);
            if (!string.IsNullOrWhiteSpace(kpi.ComparisonColumn))
                comparisonOrdinal = reader.GetOrdinal(kpi.ComparisonColumn);
            if (!string.IsNullOrWhiteSpace(kpi.DetailColumn))
                detailOrdinal = reader.GetOrdinal(kpi.DetailColumn);
        }
        catch (IndexOutOfRangeException)
        {
            throw new InvalidOperationException("Alguna columna configurada para el KPI no existe en el resultado.");
        }

        object? rawValue = await reader.IsDBNullAsync(valueOrdinal, cancellationToken) ? null : reader.GetValue(valueOrdinal);
        decimal? numericValue = null;
        if (kpi.Format != KpiValueFormat.Texto && rawValue is not null)
            numericValue = ConvertToDecimal(rawValue, kpi.ValueColumn);

        decimal? comparison = null;
        if (comparisonOrdinal.HasValue && !await reader.IsDBNullAsync(comparisonOrdinal.Value, cancellationToken))
            comparison = ConvertToDecimal(reader.GetValue(comparisonOrdinal.Value), kpi.ComparisonColumn!);

        string? detail = null;
        if (detailOrdinal.HasValue && !await reader.IsDBNullAsync(detailOrdinal.Value, cancellationToken))
            detail = Convert.ToString(reader.GetValue(detailOrdinal.Value), DisplayCulture);

        decimal? changePercent = null;
        if (numericValue.HasValue && comparison.HasValue && comparison.Value != 0)
            changePercent = (numericValue.Value - comparison.Value) / Math.Abs(comparison.Value) * 100m;

        return new KpiQueryResult(rawValue, numericValue, detail, changePercent, DateTime.UtcNow);
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
            ApplicationName = "Arx Reportes KPI"
        };
        if (!connection.IntegratedSecurity)
        {
            builder.UserID = connection.UserName;
            builder.Password = connection.Password ?? store.UnprotectPassword(connection);
        }
        return builder.ConnectionString;
    }

    private static string FormatValue(object? rawValue, decimal? numericValue, KpiDefinition kpi)
    {
        if (rawValue is null) return "Sin dato";
        var decimals = Math.Clamp(kpi.DecimalPlaces, 0, 4);
        return kpi.Format switch
        {
            KpiValueFormat.Moneda => (numericValue ?? 0m).ToString($"C{decimals}", DisplayCulture),
            KpiValueFormat.Porcentaje => (numericValue ?? 0m).ToString($"N{decimals}", DisplayCulture) + "%",
            KpiValueFormat.Numero => (numericValue ?? 0m).ToString($"N{decimals}", DisplayCulture),
            _ => rawValue switch
            {
                DateTime date => date.ToString("dd/MM/yyyy HH:mm", DisplayCulture),
                _ => Convert.ToString(rawValue, DisplayCulture) ?? "Sin dato"
            }
        };
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
            throw new InvalidOperationException("Los KPI sólo admiten consultas SELECT o CTE (WITH).");
    }

    private sealed record KpiQueryResult(
        object? Value,
        decimal? NumericValue,
        string? Detail,
        decimal? ChangePercent,
        DateTime UpdatedAtUtc);
}
