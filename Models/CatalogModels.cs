using System.ComponentModel.DataAnnotations;

namespace ArxReportes.Models;

public sealed class CatalogData
{
    public List<ConnectionDefinition> Connections { get; set; } = [];
    public List<ReportDefinition> Reports { get; set; } = [];
    public List<KpiDefinition> Kpis { get; set; } = [];
    public List<ChartDefinition> Charts { get; set; } = [];
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
    public List<Guid> AllowedKpiIds { get; set; } = [];
    public List<Guid> AllowedChartIds { get; set; } = [];
}

public sealed class ChartDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, StringLength(100)]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = "";

    [StringLength(240)]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Required]
    [Display(Name = "Conexión")]
    public Guid ConnectionId { get; set; }

    [Required]
    [Display(Name = "Consulta SQL")]
    public string Sql { get; set; } = "";

    [Required, StringLength(128)]
    [Display(Name = "Columna de etiqueta")]
    public string LabelColumn { get; set; } = "Etiqueta";

    [Required, StringLength(128)]
    [Display(Name = "Columna de valor")]
    public string ValueColumn { get; set; } = "Valor";

    [StringLength(128)]
    [Display(Name = "Columna de serie")]
    public string? SeriesColumn { get; set; }

    [Display(Name = "Tipo de gráfica")]
    public ChartType Type { get; set; } = ChartType.Barras;

    [Display(Name = "Formato de valores")]
    public ChartValueFormat Format { get; set; } = ChartValueFormat.Numero;

    [Range(0, 4)]
    [Display(Name = "Decimales")]
    public int DecimalPlaces { get; set; }

    [Display(Name = "Paleta de colores")]
    public ChartPalette Palette { get; set; } = ChartPalette.Verde;

    [Display(Name = "Tamaño en el tablero")]
    public ChartSize Size { get; set; } = ChartSize.Media;

    [Display(Name = "Mostrar leyenda")]
    public bool ShowLegend { get; set; } = true;

    [Display(Name = "Reporte relacionado")]
    public Guid? LinkedReportId { get; set; }

    [Range(1, 120)]
    [Display(Name = "Tiempo máximo (segundos)")]
    public int CommandTimeoutSeconds { get; set; } = 30;

    [Range(0, 60)]
    [Display(Name = "Caché (minutos)")]
    public int CacheMinutes { get; set; } = 5;

    [Range(0, 9999)]
    [Display(Name = "Orden")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Activa")]
    public bool IsActive { get; set; } = true;

    public Guid CacheVersion { get; set; } = Guid.NewGuid();
}

public enum ChartType
{
    Barras,
    [Display(Name = "Líneas")]
    Lineas,
    [Display(Name = "Área")]
    Area,
    Dona
}

public enum ChartValueFormat
{
    [Display(Name = "Número")]
    Numero,
    Moneda,
    Porcentaje
}

public enum ChartPalette
{
    Verde,
    Azul,
    Mixta,
    [Display(Name = "Cálida")]
    Calida,
    Grafito
}

public enum ChartSize
{
    Media,
    [Display(Name = "Ancho completo")]
    Completa
}

public sealed class KpiDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, StringLength(100)]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = "";

    [StringLength(240)]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Required]
    [Display(Name = "Conexión")]
    public Guid ConnectionId { get; set; }

    [Required]
    [Display(Name = "Consulta SQL")]
    public string Sql { get; set; } = "";

    [Required, StringLength(128)]
    [Display(Name = "Columna de valor")]
    public string ValueColumn { get; set; } = "Valor";

    [StringLength(128)]
    [Display(Name = "Columna de comparación")]
    public string? ComparisonColumn { get; set; }

    [StringLength(128)]
    [Display(Name = "Columna de detalle")]
    public string? DetailColumn { get; set; }

    [StringLength(80)]
    [Display(Name = "Etiqueta de comparación")]
    public string? ComparisonLabel { get; set; } = "vs. referencia";

    [Display(Name = "Formato")]
    public KpiValueFormat Format { get; set; } = KpiValueFormat.Numero;

    [Range(0, 4)]
    [Display(Name = "Decimales")]
    public int DecimalPlaces { get; set; }

    [Display(Name = "Un valor menor es mejor")]
    public bool LowerIsBetter { get; set; }

    [Display(Name = "Color")]
    public KpiTone Tone { get; set; } = KpiTone.Verde;

    [Display(Name = "Icono")]
    public KpiIcon Icon { get; set; } = KpiIcon.Indicador;

    [Display(Name = "Reporte relacionado")]
    public Guid? LinkedReportId { get; set; }

    [Range(1, 120)]
    [Display(Name = "Tiempo máximo (segundos)")]
    public int CommandTimeoutSeconds { get; set; } = 30;

    [Range(0, 60)]
    [Display(Name = "Caché (minutos)")]
    public int CacheMinutes { get; set; } = 2;

    [Range(0, 9999)]
    [Display(Name = "Orden")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;

    public Guid CacheVersion { get; set; } = Guid.NewGuid();
}

public enum KpiValueFormat
{
    [Display(Name = "Número")]
    Numero,
    Moneda,
    Porcentaje,
    Texto
}

public enum KpiTone
{
    Verde,
    Azul,
    [Display(Name = "Ámbar")]
    Ambar,
    Rojo,
    Grafito
}

public enum KpiIcon
{
    Indicador,
    Dinero,
    Documento,
    Usuarios,
    Alerta,
    Porcentaje
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
    public List<ReportKpiDefinition> ReportKpis { get; set; } = [];
    public List<ReportDrilldownDefinition> Drilldowns { get; set; } = [];
}

public sealed class ReportDrilldownDefinition
{
    [Required, StringLength(128)]
    [Display(Name = "Columna clicable")]
    public string SourceColumn { get; set; } = "";

    [StringLength(128)]
    [Display(Name = "Columna que enviará el valor")]
    public string? ValueColumn { get; set; }

    [Display(Name = "Reporte destino")]
    public Guid TargetReportId { get; set; }

    /// <summary>Nombre portable para volver a vincular el destino al importar el reporte.</summary>
    [StringLength(120)]
    public string? TargetReportName { get; set; }

    [Required, StringLength(128)]
    [RegularExpression(@"^[A-Za-z_][A-Za-z0-9_]*$", ErrorMessage = "Use letras, números y guion bajo; no incluya @.")]
    [Display(Name = "Parámetro destino")]
    public string TargetParameterName { get; set; } = "";

    [Display(Name = "Ejecutar automáticamente")]
    public bool AutoExecute { get; set; } = true;

    [Display(Name = "Abrir en otra pestaña")]
    public bool OpenInNewTab { get; set; }
}

public sealed class ReportKpiDefinition
{
    [Required, StringLength(80)]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = "";

    [StringLength(128)]
    [Display(Name = "Columna")]
    public string? ColumnName { get; set; }

    [Display(Name = "Cálculo"), EnumDataType(typeof(ReportKpiOperation))]
    public ReportKpiOperation Operation { get; set; } = ReportKpiOperation.Suma;

    [Display(Name = "Formato"), EnumDataType(typeof(ReportKpiValueFormat))]
    public ReportKpiValueFormat Format { get; set; } = ReportKpiValueFormat.Numero;

    [Range(0, 4)]
    [Display(Name = "Decimales")]
    public int DecimalPlaces { get; set; }

    [Display(Name = "Color"), EnumDataType(typeof(KpiTone))]
    public KpiTone Tone { get; set; } = KpiTone.Verde;

    [Display(Name = "Icono"), EnumDataType(typeof(KpiIcon))]
    public KpiIcon Icon { get; set; } = KpiIcon.Indicador;
}

public enum ReportKpiOperation
{
    Suma,
    Promedio,
    Conteo,
    [Display(Name = "Conteo distinto")]
    ConteoDistinto,
    [Display(Name = "Mínimo")]
    Minimo,
    [Display(Name = "Máximo")]
    Maximo
}

public enum ReportKpiValueFormat
{
    [Display(Name = "Número")]
    Numero,
    Moneda,
    Porcentaje
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
