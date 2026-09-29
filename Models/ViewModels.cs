using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace ArxReportes.Models;

public sealed class ReportEditViewModel
{
    public ReportDefinition Report { get; set; } = new();
    public List<SelectListItem> Connections { get; set; } = [];

    [Display(Name = "Valores de la primera columna que se destacarán")]
    public string? HighlightFirstColumnValuesText { get; set; }
}

public sealed class ReportRunViewModel
{
    public required ReportDefinition Report { get; set; }
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public ReportResult? Result { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, List<ReportLookupOption>> LookupOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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
    public List<ReportPermissionItem> Reports { get; set; } = [];
}

public sealed record ReportPermissionItem(Guid Id, string Name, bool IsActive);
