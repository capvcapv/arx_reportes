using System.Text.Json.Serialization;

namespace ArxReportes.Models;

/// <summary>
/// Formato portable de intercambio. Deliberadamente no incluye credenciales,
/// contraseñas ni identificadores internos de la instalación de origen.
/// </summary>
public sealed class ReportPackage
{
    public int SchemaVersion { get; set; } = 3;
    public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
    public string SourceConnectionName { get; set; } = "";
    public PortableReport Report { get; set; } = new();
}

public sealed class PortableReport
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Sql { get; set; } = "";
    public int CommandTimeoutSeconds { get; set; } = 60;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool ShowColumnTotals { get; set; }
    public List<string> HighlightFirstColumnValues { get; set; } = [];
    public List<PortableReportParameter> Parameters { get; set; } = [];
    public List<PortableReportKpi> ReportKpis { get; set; } = [];
    public List<PortableReportDrilldown> Drilldowns { get; set; } = [];
}

public sealed class PortableReportDrilldown
{
    public string SourceColumn { get; set; } = "";
    public string? ValueColumn { get; set; }
    public string TargetReportName { get; set; } = "";
    public string TargetParameterName { get; set; } = "";
    public bool AutoExecute { get; set; } = true;
    public bool OpenInNewTab { get; set; }
}

public sealed class PortableReportKpi
{
    public string Name { get; set; } = "";
    public string? ColumnName { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReportKpiOperation Operation { get; set; } = ReportKpiOperation.Suma;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReportKpiValueFormat Format { get; set; } = ReportKpiValueFormat.Numero;

    public int DecimalPlaces { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KpiTone Tone { get; set; } = KpiTone.Verde;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KpiIcon Icon { get; set; } = KpiIcon.Indicador;
}

public sealed class PortableReportParameter
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReportParameterType Type { get; set; }

    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public string? Placeholder { get; set; }
    public string? LookupSql { get; set; }
    public string? LookupValueColumn { get; set; }
    public string? LookupDisplayColumns { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LookupValueType LookupValueType { get; set; } = LookupValueType.Texto;
}
