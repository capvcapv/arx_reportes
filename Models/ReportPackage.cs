using System.Text.Json.Serialization;

namespace ArxReportes.Models;

/// <summary>
/// Formato portable de intercambio. Deliberadamente no incluye credenciales,
/// contraseñas ni identificadores internos de la instalación de origen.
/// </summary>
public sealed class ReportPackage
{
    public int SchemaVersion { get; set; } = 1;
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
