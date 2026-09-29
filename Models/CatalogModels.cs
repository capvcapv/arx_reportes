using System.ComponentModel.DataAnnotations;

namespace ArxReportes.Models;

public sealed class CatalogData
{
    public List<ConnectionDefinition> Connections { get; set; } = [];
    public List<ReportDefinition> Reports { get; set; } = [];
    public List<UserDefinition> Users { get; set; } = [];
}

public sealed class UserDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<Guid> AllowedReportIds { get; set; } = [];
}

public sealed class ConnectionDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, StringLength(100)]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = "";

    [Required, StringLength(200)]
    [Display(Name = "Servidor / instancia")]
    public string Server { get; set; } = "";

    [Required, StringLength(150)]
    [Display(Name = "Base de datos")]
    public string Database { get; set; } = "";

    [StringLength(150)]
    [Display(Name = "Usuario SQL")]
    public string? UserName { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string? Password { get; set; }

    public string? ProtectedPassword { get; set; }

    [Display(Name = "Autenticación integrada")]
    public bool IntegratedSecurity { get; set; }

    [Display(Name = "Cifrar conexión")]
    public bool Encrypt { get; set; } = true;

    [Display(Name = "Confiar en certificado del servidor")]
    public bool TrustServerCertificate { get; set; }

    [Display(Name = "Activa")]
    public bool IsActive { get; set; } = true;
}

public sealed class ReportDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, StringLength(120)]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = "";

    [StringLength(500)]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Required]
    [Display(Name = "Conexión")]
    public Guid ConnectionId { get; set; }

    [Required]
    [Display(Name = "Sentencia SQL")]
    public string Sql { get; set; } = "";

    [Range(1, 600)]
    [Display(Name = "Tiempo máximo (segundos)")]
    public int CommandTimeoutSeconds { get; set; } = 60;

    [Range(0, 9999)]
    [Display(Name = "Orden")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Mostrar sumatorias de columnas numéricas")]
    public bool ShowColumnTotals { get; set; }

    /// <summary>
    /// Valores que resaltan una fila cuando coinciden exactamente con su primera columna.
    /// La comparación ignora mayúsculas, minúsculas y espacios exteriores.
    /// </summary>
    public List<string> HighlightFirstColumnValues { get; set; } = [];

    public List<ReportParameterDefinition> Parameters { get; set; } = [];
}

public sealed class ReportParameterDefinition
{
    [Required, RegularExpression(@"^[A-Za-z_][A-Za-z0-9_]*$", ErrorMessage = "Use letras, números y guion bajo; no incluya @.")]
    [Display(Name = "Parámetro")]
    public string Name { get; set; } = "";

    [Required, StringLength(100)]
    [Display(Name = "Etiqueta")]
    public string Label { get; set; } = "";

    [Display(Name = "Tipo")]
    public ReportParameterType Type { get; set; }

    [Display(Name = "Obligatorio")]
    public bool IsRequired { get; set; }

    [StringLength(500)]
    [Display(Name = "Valor predeterminado")]
    public string? DefaultValue { get; set; }

    [StringLength(150)]
    [Display(Name = "Ayuda")]
    public string? Placeholder { get; set; }

    [Display(Name = "Consulta de opciones")]
    public string? LookupSql { get; set; }

    [StringLength(128)]
    [Display(Name = "Columna de valor")]
    public string? LookupValueColumn { get; set; }

    [StringLength(500)]
    [Display(Name = "Columnas visibles")]
    public string? LookupDisplayColumns { get; set; }

    [Display(Name = "Tipo del valor")]
    public LookupValueType LookupValueType { get; set; } = LookupValueType.Texto;
}

public enum ReportParameterType
{
    Texto,
    Entero,
    Decimal,
    Fecha,
    FechaHora,
    Booleano,
    Consulta
}

public enum LookupValueType
{
    Texto,
    Entero,
    Decimal,
    Booleano
}
