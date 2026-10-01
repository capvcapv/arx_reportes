using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace ArxReportes.Models;

public sealed class ReportEditViewModel
{
    public ReportDefinition Report { get; set; } = new();
    public List<SelectListItem> Connections { get; set; } = [];
    public List<ReportTargetOption> TargetReports { get; set; } = [];

    [Display(Name = "Valores de la primera columna que se destacarán")]
    public string? HighlightFirstColumnValuesText { get; set; }
}

public sealed class ReportRunViewModel
{
    public required ReportDefinition Report { get; set; }
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public ReportResult? Result { get; set; }
    public List<ReportKpiCardViewModel> Kpis { get; set; } = [];
    public string? Error { get; set; }
    public Dictionary<string, List<ReportLookupOption>> LookupOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ReportDrilldownBindingViewModel> Drilldowns { get; set; } = [];
}

public sealed class ReportTargetOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public List<string> ParameterNames { get; set; } = [];
}

public sealed class ReportDrilldownEditRowViewModel
{
    public ReportDrilldownDefinition Drilldown { get; set; } = new();
    public int Index { get; set; }
    public List<ReportTargetOption> TargetReports { get; set; } = [];
}

public sealed class ReportDrilldownBindingViewModel
{
    public int ClickableColumnIndex { get; set; }
    public int ValueColumnIndex { get; set; }
    public Guid TargetReportId { get; set; }
    public string TargetReportName { get; set; } = "";
    public string TargetParameterName { get; set; } = "";
    public ReportParameterType TargetParameterType { get; set; }
    public LookupValueType TargetLookupValueType { get; set; }
    public bool AutoExecute { get; set; }
    public bool OpenInNewTab { get; set; }
}

public sealed class ReportKpiCardViewModel
{
    public required ReportKpiDefinition Definition { get; set; }
    public decimal? NumericValue { get; set; }
    public string DisplayValue { get; set; } = "—";
    public string OperationLabel { get; set; } = "";
    public string? Error { get; set; }
}

public sealed class DashboardViewModel
{
    public List<ReportDefinition> Reports { get; set; } = [];
    public List<KpiCardViewModel> Kpis { get; set; } = [];
    public List<ChartCardViewModel> Charts { get; set; } = [];
}

public sealed class ChartCardViewModel
{
    public required ChartDefinition Definition { get; set; }
    public List<string> Labels { get; set; } = [];
    public List<ChartDataSetViewModel> DataSets { get; set; } = [];
    public DateTime? UpdatedAtUtc { get; set; }
    public bool Truncated { get; set; }
    public string? Error { get; set; }
    public Guid? AccessibleReportId { get; set; }
}

public sealed class ChartDataSetViewModel
{
    public string Name { get; set; } = "Valor";
    public List<decimal?> Values { get; set; } = [];
}

public sealed class KpiCardViewModel
{
    public required KpiDefinition Definition { get; set; }
    public string? DisplayValue { get; set; }
    public string? Detail { get; set; }
    public decimal? ChangePercent { get; set; }
    public bool? IsPositive { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? Error { get; set; }
    public Guid? AccessibleReportId { get; set; }
}

public sealed class KpiEditViewModel
{
    public KpiDefinition Kpi { get; set; } = new();
    public List<SelectListItem> Connections { get; set; } = [];
    public List<SelectListItem> Reports { get; set; } = [];
}

public sealed class ChartEditViewModel
{
    public ChartDefinition Chart { get; set; } = new();
    public List<SelectListItem> Connections { get; set; } = [];
    public List<SelectListItem> Reports { get; set; } = [];
}

public sealed record ReportLookupOption(string Value, string Label);

public sealed class ReportResult
{
    public List<string> Columns { get; set; } = [];
    public List<List<object?>> Rows { get; set; } = [];
    public List<decimal?> ColumnTotals { get; set; } = [];
    public bool Truncated { get; set; }
    public TimeSpan Elapsed { get; set; }
}

public sealed class AdminLoginViewModel
{
    public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

public sealed class UserLoginViewModel
{
    [Required]
    [Display(Name = "Usuario")]
    public string UserName { get; set; } = "";

    [Required, DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public sealed class UserEditViewModel
{
    public Guid Id { get; set; }

    [Required, StringLength(80)]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Usa letras, números, punto, guion o guion bajo.")]
    [Display(Name = "Usuario")]
    public string UserName { get; set; } = "";

    [Required, StringLength(120)]
    [Display(Name = "Nombre")]
    public string DisplayName { get; set; } = "";

    [DataType(DataType.Password), MinLength(8)]
    [Display(Name = "Contraseña")]
    public string? Password { get; set; }

    [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string? ConfirmPassword { get; set; }

    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;

    public List<Guid> AllowedReportIds { get; set; } = [];
    public List<Guid> AllowedKpiIds { get; set; } = [];
    public List<Guid> AllowedChartIds { get; set; } = [];
    public List<ReportPermissionItem> Reports { get; set; } = [];
    public List<KpiPermissionItem> Kpis { get; set; } = [];
    public List<ChartPermissionItem> Charts { get; set; } = [];
}

public sealed record ReportPermissionItem(Guid Id, string Name, bool IsActive);
public sealed record KpiPermissionItem(Guid Id, string Name, bool IsActive);
public sealed record ChartPermissionItem(Guid Id, string Name, bool IsActive);
