# Arx Reportes

Aplicación web ASP.NET Core 9 para publicar consultas de SQL Server como reportes parametrizados, mostrarlos en una tabla HTML y exportarlos a Excel.

## Indicadores KPI

En **Administración → KPI** puedes crear indicadores para la pantalla principal. Cada consulta debe comenzar con `SELECT` o `WITH`, devolver una sola fila y exponer una columna principal, por ejemplo:

```sql
SELECT
    SUM(CTOTAL) AS Valor,
    COUNT(*) AS Detalle
FROM admDocumentos
WHERE CIDDOCUMENTODE = 4
  AND CCANCELADO = 0;
```

- Configura `Valor` como columna de valor y, opcionalmente, otra columna numérica como referencia para calcular la variación porcentual.
- Puedes mostrar el resultado como número, moneda, porcentaje o texto; elegir color, icono, decimales y un reporte relacionado.
- La caché evita ejecutar todos los KPI en cada carga. Se recomienda un intervalo de 2 a 5 minutos.
- Un fallo en un KPI no bloquea los demás indicadores ni el catálogo de reportes.
- En **Administración → Usuarios** selecciona qué KPI y reportes puede consultar cada cuenta.
- Usa consultas ligeras, un máximo razonable de indicadores y una cuenta SQL exclusivamente de lectura.

## Gráficas

En **Administración → Gráficas** puedes crear visualizaciones de barras, líneas, área o dona. La consulta debe devolver una columna de etiqueta, una columna numérica y, opcionalmente, una columna de serie:

```sql
SELECT
    CAST(MONTH(CFECHA) AS VARCHAR(2)) AS Etiqueta,
    SUM(CTOTAL) AS Valor,
    CAST(YEAR(CFECHA) AS VARCHAR(4)) AS Serie
FROM admDocumentos
WHERE CCANCELADO = 0
GROUP BY YEAR(CFECHA), MONTH(CFECHA)
ORDER BY YEAR(CFECHA), MONTH(CFECHA);
```

- Configura `Etiqueta`, `Valor` y `Serie` con los alias devueltos por la consulta.
- Puedes elegir formato numérico, moneda o porcentaje; paleta, tamaño medio o completo, leyenda y reporte relacionado.
- Las gráficas son interactivas, adaptables a móvil y se dibujan localmente, por lo que no necesitan acceso a internet.
- La tabla desplegable debajo de cada gráfica conserva acceso a los valores exactos.
- La caché, el límite de filas y el máximo de series se controlan en la sección `Charts` de `appsettings.json`.
- Asigna cada gráfica a los usuarios autorizados desde **Administración → Usuarios**.

## Puesta en marcha

1. Cambia la clave administrativa. En producción usa la variable de entorno `Admin__Password`; el valor de `appsettings.json` es sólo inicial.
2. Ejecuta `dotnet restore` y `dotnet run`.
3. Abre la URL indicada en consola y entra a **Administración**.
4. Crea una conexión SQL Server y usa **Probar**.
   En el formulario puedes usar **Consultar bases** para ver únicamente las bases de datos a las que tiene acceso el usuario SQL capturado.
5. Crea un reporte con una consulta `SELECT` o `WITH`. Para `WHERE ClienteId = @ClienteId`, registra un parámetro llamado `ClienteId`.

### Parámetros obtenidos desde una consulta

Selecciona el tipo de parámetro **Consulta** cuando el usuario deba elegir un registro de un catálogo SQL. Configura:

- **Consulta para llenar la lista:** por ejemplo `SELECT CIDCLIENTEPROVEEDOR, CCODIGOCLIENTE, CRAZONSOCIAL FROM admClientes ORDER BY CRAZONSOCIAL`.
- **Columna de valor:** `CIDCLIENTEPROVEEDOR`; este dato se enviará a `@ClienteId`.
- **Columnas visibles:** `CCODIGOCLIENTE, CRAZONSOCIAL`; se mostrarán juntas en la lista.
- **Tipo del valor:** utiliza `Entero` cuando el identificador sea numérico.

La consulta del reporte puede usarlo normalmente: `SELECT * FROM admDocumentos WHERE IDCLIENTE = @ClienteId`. Las consultas de catálogo se limitan a `Reports:MaximumLookupRows`.

### KPI dentro de un reporte

En el editor de cada reporte puedes agregar hasta 12 KPI que se muestran encima de la tabla después de ejecutar la consulta. Se calculan con el mismo resultado y respetan todos los parámetros capturados por el usuario.

- Operaciones disponibles: suma, promedio, conteo, conteo distinto, mínimo y máximo.
- Para suma, promedio, conteo distinto, mínimo y máximo indica el nombre exacto de una columna devuelta por la consulta.
- Para contar todas las filas selecciona `Conteo` y deja la columna vacía; si indicas una columna sólo contará sus valores no nulos.
- Cada KPI permite elegir número, moneda o porcentaje, decimales, color e icono.
- Si el resultado se limita por `Reports:MaximumRows`, la pantalla avisa que los KPI se calcularon únicamente con las filas mostradas.
- Al exportar a Excel, los KPI aparecen siempre en una franja superior de la misma hoja, antes de los encabezados y filas del reporte.

### Desglose entre reportes

En el editor de reportes puedes convertir una columna del resultado en un enlace hacia otro reporte. Cada enlace permite configurar:

- **Columna clicable:** el alias exacto que verá y pulsará el usuario, por ejemplo `Cliente` o `Folio`.
- **Valor a enviar:** otra columna de la misma fila, como `ClienteId`; si se deja vacía se usa el valor de la columna clicable.
- **Reporte y parámetro destino:** el reporte que mostrará el detalle y el parámetro que recibirá el valor.
- **Ejecución automática:** abre el detalle ya consultado o solamente deja su filtro precargado.
- **Nueva pestaña:** conserva abierto el reporte principal mientras se consulta el detalle.

El enlace sólo aparece cuando ambas columnas existen en el resultado, el reporte destino está activo y el usuario tiene permiso para ejecutarlo. Los enlaces de desglose también viajan en los archivos JSON; al importar se vuelven a vincular por el nombre del reporte destino.

## Transferir reportes

- Usa **Guía JSON para IA** para descargar el contrato completo, todos los tipos de parámetros, KPI, enlaces de desglose y un ejemplo. Puedes entregarlo a una IA para que genere archivos compatibles con ARX Reportes.
- En **Administrar → Reportes**, usa **JSON** para descargar individualmente la definición de un reporte.
- El nombre del archivo incluye fecha, hora y segundos para evitar reemplazos accidentales.
- Usa **Importar JSON** para subirlo en esta u otra instalación, ya sea seleccionando un archivo o pegando directamente el texto JSON. Puedes conservar la conexión indicada en el contenido o elegir otra conexión destino.
- El reporte importado obtiene un identificador nuevo y una marca de fecha/hora en su nombre. Sus KPI viajan en el mismo archivo; nunca se incluyen usuarios, contraseñas ni cadenas de conexión.

## Seguridad y persistencia

- Usa una cuenta de SQL Server con permisos exclusivamente de lectura. La validación de texto no sustituye los permisos de base de datos.
- Las contraseñas SQL se cifran mediante ASP.NET Core Data Protection.
- Las contraseñas de los usuarios finales se almacenan mediante hash seguro y no pueden recuperarse; un administrador sólo puede reemplazarlas.
- En **Administración → Usuarios** puedes crear, desactivar o eliminar cuentas y seleccionar individualmente los reportes permitidos.
- El catálogo y las rutas directas validan los permisos del usuario en cada solicitud.
- El catálogo se guarda en `App_Data/catalog.json` y las llaves de cifrado en `App_Data/keys`; ambos deben persistirse y respaldarse en producción.
- No publiques `App_Data`, la clave administrativa ni archivos de configuración con secretos en control de versiones.
- El resultado está limitado por `Reports:MaximumRows` (10,000 de forma predeterminada).

## Publicación

```bash
dotnet publish -c Release -o ./publish
```

Despliega la carpeta `publish` en IIS, Azure App Service, Linux con systemd/reverse proxy o un contenedor. La identidad del proceso necesita escritura en `App_Data`.
