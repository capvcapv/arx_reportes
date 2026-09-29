using System.Data;
using System.Diagnostics;
using System.Globalization;
using ArxReportes.Models;
using Microsoft.Data.SqlClient;

namespace ArxReportes.Services;

public sealed class SqlReportRunner(CatalogStore store, IConfiguration configuration)
{
    public async Task<List<string>> GetDatabasesAsync(ConnectionDefinition connection, CancellationToken cancellationToken = default)
    {
        var connectionString = new SqlConnectionStringBuilder(BuildConnectionString(connection))
        {
            InitialCatalog = "master"
        }.ConnectionString;

        var databases = new List<string>();
        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT [name] FROM sys.databases WHERE [state] = 0 AND HAS_DBACCESS([name]) = 1 ORDER BY [name];",
            sqlConnection)
        {
            CommandTimeout = 15
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            databases.Add(reader.GetString(0));
        return databases;
    }

    public async Task TestConnectionAsync(ConnectionDefinition connection, CancellationToken cancellationToken = default)
    {
        await using var sqlConnection = new SqlConnection(BuildConnectionString(connection));
        await sqlConnection.OpenAsync(cancellationToken);
    }

    public async Task<ReportResult> ExecuteAsync(
        ReportDefinition report,
        ConnectionDefinition connection,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default)
    {
        ValidateReadOnlyQuery(report.Sql);
        var result = new ReportResult();
        var stopwatch = Stopwatch.StartNew();
        var maximumRows = Math.Clamp(configuration.GetValue("Reports:MaximumRows", 10000), 1, 1000000);

        await using var sqlConnection = new SqlConnection(BuildConnectionString(connection));
        await sqlConnection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(report.Sql, sqlConnection)
        {
            CommandTimeout = report.CommandTimeoutSeconds,
            CommandType = CommandType.Text
        };

        foreach (var definition in report.Parameters)
        {
            values.TryGetValue(definition.Name, out var rawValue);
            command.Parameters.Add(CreateParameter(definition, rawValue));
        }

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        var numericColumns = new bool[reader.FieldCount];
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var name = reader.GetName(index);
            result.Columns.Add(string.IsNullOrWhiteSpace(name) ? $"Columna {index + 1}" : name);
            numericColumns[index] = report.ShowColumnTotals && IsNumericType(reader.GetFieldType(index));
            result.ColumnTotals.Add(numericColumns[index] ? 0m : null);
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            if (result.Rows.Count >= maximumRows)
            {
                result.Truncated = true;
                break;
            }

            var row = new List<object?>(reader.FieldCount);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var value = await reader.IsDBNullAsync(index, cancellationToken) ? null : reader.GetValue(index);
                row.Add(value);
                if (numericColumns[index] && value is not null)
                {
                    try
                    {
                        result.ColumnTotals[index] = checked((result.ColumnTotals[index] ?? 0m) + Convert.ToDecimal(value, CultureInfo.InvariantCulture));
                    }
                    catch (Exception exception) when (exception is OverflowException or FormatException or InvalidCastException)
                    {
                        numericColumns[index] = false;
                        result.ColumnTotals[index] = null;
                    }
                }
            }
            result.Rows.Add(row);
        }

        stopwatch.Stop();
        result.Elapsed = stopwatch.Elapsed;
        return result;
    }

    public async Task<List<ReportLookupOption>> GetLookupOptionsAsync(
        ReportParameterDefinition definition,
        ConnectionDefinition connection,
        CancellationToken cancellationToken = default)
    {
        if (definition.Type != ReportParameterType.Consulta)
            return [];
        if (string.IsNullOrWhiteSpace(definition.LookupSql) || string.IsNullOrWhiteSpace(definition.LookupValueColumn))
            throw new InvalidOperationException($"El catálogo '{definition.Label}' no tiene configurada su consulta o columna de valor.");

        ValidateReadOnlyQuery(definition.LookupSql);
        var displayColumns = (definition.LookupDisplayColumns ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (displayColumns.Length == 0)
            displayColumns = [definition.LookupValueColumn];

        var maximumRows = Math.Clamp(configuration.GetValue("Reports:MaximumLookupRows", 2000), 1, 10000);
        var options = new List<ReportLookupOption>();
        await using var sqlConnection = new SqlConnection(BuildConnectionString(connection));
        await sqlConnection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(definition.LookupSql, sqlConnection)
        {
            CommandTimeout = 30,
            CommandType = CommandType.Text
        };
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

        int valueOrdinal;
        int[] displayOrdinals;
        try
        {
            valueOrdinal = reader.GetOrdinal(definition.LookupValueColumn);
            displayOrdinals = displayColumns.Select(reader.GetOrdinal).ToArray();
        }
        catch (IndexOutOfRangeException)
        {
            throw new InvalidOperationException($"Revisa las columnas configuradas para el catálogo '{definition.Label}'. Alguna no existe en el resultado de la consulta.");
        }

        while (options.Count < maximumRows && await reader.ReadAsync(cancellationToken))
        {
            if (await reader.IsDBNullAsync(valueOrdinal, cancellationToken)) continue;
            var rawValue = reader.GetValue(valueOrdinal);
            var value = Convert.ToString(rawValue, CultureInfo.InvariantCulture) ?? "";
            var labelParts = new List<string>();
            foreach (var ordinal in displayOrdinals)
            {
                if (await reader.IsDBNullAsync(ordinal, cancellationToken)) continue;
                var part = Convert.ToString(reader.GetValue(ordinal), CultureInfo.CurrentCulture);
                if (!string.IsNullOrWhiteSpace(part)) labelParts.Add(part);
            }
            options.Add(new ReportLookupOption(value, labelParts.Count > 0 ? string.Join(" · ", labelParts) : value));
        }
        return options;
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
            ApplicationName = "Arx Reportes"
        };
        if (!connection.IntegratedSecurity)
        {
            builder.UserID = connection.UserName;
            builder.Password = connection.Password ?? store.UnprotectPassword(connection);
        }
        return builder.ConnectionString;
    }

    private static SqlParameter CreateParameter(ReportParameterDefinition definition, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (definition.IsRequired)
                throw new InvalidOperationException($"El parámetro '{definition.Label}' es obligatorio.");
            return new SqlParameter("@" + definition.Name, GetSqlType(definition)) { Value = DBNull.Value };
        }

        object value = definition.Type switch
        {
            ReportParameterType.Texto => rawValue,
            ReportParameterType.Entero => long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.CurrentCulture, out var integer)
                ? integer : throw new InvalidOperationException($"'{definition.Label}' debe ser un número entero."),
            ReportParameterType.Decimal => TryDecimal(rawValue, out var number)
                ? number : throw new InvalidOperationException($"'{definition.Label}' debe ser un número decimal."),
            ReportParameterType.Fecha => DateTime.TryParse(rawValue, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date)
                ? date.Date : throw new InvalidOperationException($"'{definition.Label}' debe ser una fecha válida."),
            ReportParameterType.FechaHora => DateTime.TryParse(rawValue, CultureInfo.CurrentCulture, DateTimeStyles.None, out var dateTime)
                ? dateTime : throw new InvalidOperationException($"'{definition.Label}' debe ser una fecha y hora válidas."),
            ReportParameterType.Booleano => bool.TryParse(rawValue, out var boolean)
                ? boolean : rawValue is "1" or "si" or "sí",
            ReportParameterType.Consulta => ParseLookupValue(definition.LookupValueType, definition.Label, rawValue),
            _ => rawValue
        };
        return new SqlParameter("@" + definition.Name, GetSqlType(definition)) { Value = value };
    }

    private static bool TryDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result) ||
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static bool IsNumericType(Type type) => Type.GetTypeCode(Nullable.GetUnderlyingType(type) ?? type) switch
    {
        TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or
        TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or
        TypeCode.Single or TypeCode.Double or TypeCode.Decimal => true,
        _ => false
    };

    private static object ParseLookupValue(LookupValueType type, string label, string rawValue) => type switch
    {
        LookupValueType.Entero => long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
            ? integer : throw new InvalidOperationException($"El valor seleccionado para '{label}' no es un entero válido."),
        LookupValueType.Decimal => decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number : throw new InvalidOperationException($"El valor seleccionado para '{label}' no es un decimal válido."),
        LookupValueType.Booleano => bool.TryParse(rawValue, out var boolean)
            ? boolean : rawValue == "1",
        _ => rawValue
    };

    private static SqlDbType GetSqlType(ReportParameterDefinition definition)
    {
        if (definition.Type == ReportParameterType.Consulta)
        {
            return definition.LookupValueType switch
            {
                LookupValueType.Entero => SqlDbType.BigInt,
                LookupValueType.Decimal => SqlDbType.Decimal,
                LookupValueType.Booleano => SqlDbType.Bit,
                _ => SqlDbType.NVarChar
            };
        }
        return definition.Type switch
    {
        ReportParameterType.Entero => SqlDbType.BigInt,
        ReportParameterType.Decimal => SqlDbType.Decimal,
        ReportParameterType.Fecha => SqlDbType.Date,
        ReportParameterType.FechaHora => SqlDbType.DateTime2,
        ReportParameterType.Booleano => SqlDbType.Bit,
        _ => SqlDbType.NVarChar
    };
    }

    private static void ValidateReadOnlyQuery(string sql)
    {
        var normalized = sql.TrimStart().TrimStart('\uFEFF');
        if (!(normalized.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
              normalized.StartsWith("with", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Por seguridad, los reportes sólo admiten consultas SELECT o CTE (WITH). Use además un usuario SQL de sólo lectura.");
    }
}
