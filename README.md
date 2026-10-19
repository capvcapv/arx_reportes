# Arx Reportes

Aplicación web ASP.NET Core 9 para publicar consultas de SQL Server como reportes parametrizados, mostrarlos en una tabla HTML y exportarlos a Excel.

## Guía rápida: desde la descarga hasta el primer reporte

### 1. Requisitos

- Una computadora o servidor con acceso a la instancia de SQL Server donde están las empresas de CONTPAQi Comercial.
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) si ejecutarás el proyecto desde el código fuente.
- Una cuenta de SQL Server con permisos exclusivamente de lectura sobre la base de datos de la empresa.
- El puerto `5080` disponible si otros equipos de la red abrirán la aplicación.

> Antes de comenzar, respalda la información de la empresa. ARX Reportes sólo debe conectarse con un usuario SQL de lectura.

### 2. Descargar el proyecto

Puedes abrir [github.com/capvcapv/arx_reportes](https://github.com/capvcapv/arx_reportes), pulsar **Code → Download ZIP** y descomprimir el archivo, o clonarlo con Git:

```bash
git clone https://github.com/capvcapv/arx_reportes.git
cd arx_reportes
```

### 3. Ejecutarlo desde el código fuente

Abre una terminal dentro de la carpeta del proyecto y ejecuta:

```bash
dotnet restore
dotnet run
```

Después abre en el navegador:

```text
http://localhost:5080
```

Para entrar como administrador utiliza:

```text
http://localhost:5080/admin/acceso
```

La clave inicial se encuentra en la sección `Admin:Password` de `appsettings.json`. Cámbiala antes de utilizar la aplicación en producción; también puedes definirla mediante la variable de entorno `Admin__Password`.

### 4. Ejecutar una versión ya publicada en Windows

Copia completa la carpeta publicada al servidor, abre PowerShell o CMD dentro de ella y ejecuta:

```bat
ArxReportes.exe --urls "http://0.0.0.0:5080"
```

En el mismo servidor abre `http://localhost:5080`. Desde otro equipo de la red utiliza la IP del servidor, por ejemplo `http://192.168.1.50:5080`, y permite el puerto TCP `5080` en el Firewall de Windows.

No copies únicamente `ArxReportes.exe`: conserva todos los archivos de la publicación. La carpeta `App_Data` contiene conexiones, reportes, usuarios y llaves de cifrado; debe tener permisos de escritura y conservarse al actualizar la aplicación.

### 5. Conectar una empresa de CONTPAQi Comercial

1. Entra a **Administración → Conexiones → Nueva conexión**.
2. Captura el servidor o instancia SQL, por ejemplo `SERVIDOR\COMPAC`.
3. Indica el usuario y la contraseña SQL de sólo lectura.
4. Pulsa **Consultar bases** y selecciona la base correspondiente a la empresa de CONTPAQi Comercial.
5. Usa **Probar conexión** y, si la prueba es correcta, guarda la conexión.

El nombre de la base depende de la instalación y de la empresa creada en CONTPAQi. Evita seleccionar la base de otra empresa únicamente porque tenga un nombre parecido.

### 6. Crear un reporte de clientes

Entra a **Administración → Reportes → Nuevo reporte** y utiliza esta configuración:

- **Nombre:** `Listado de clientes`
- **Descripción:** `Clientes registrados en CONTPAQi Comercial`
- **Conexión:** la empresa configurada en el paso anterior
- **Activo:** sí
- **Parámetros:** ninguno

Consulta SQL:

```sql
SELECT
    C.CIDCLIENTEPROVEEDOR AS IdCliente,
    C.CCODIGOCLIENTE AS Codigo,
    C.CRAZONSOCIAL AS Cliente,
    C.CRFC AS RFC
FROM admClientes AS C
WHERE C.CTIPOCLIENTE IN (1, 2)
ORDER BY C.CRAZONSOCIAL;
```

Guarda el reporte, vuelve al inicio y abre **Listado de clientes**. Desde ahí podrás ejecutarlo y exportar el resultado a Excel.

Las estructuras de CONTPAQi pueden variar entre versiones. Si alguna columna no existe en tu instalación, primero revisa una muestra con:

```sql
SELECT TOP 10 *
FROM admClientes;
```

Después ajusta los nombres de las columnas del ejemplo. No utilices `INSERT`, `UPDATE`, `DELETE`, procedimientos de modificación ni una cuenta SQL con permisos de escritura.

### 7. Publicarlo para usuarios finales

1. Crea las cuentas desde **Administración → Usuarios**.
2. Asigna a cada cuenta los reportes, KPI y gráficas que podrá consultar.
3. Comparte la dirección `http://IP-DEL-SERVIDOR:5080`.
4. Respalda periódicamente la carpeta `App_Data`.

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

## Configuración de reportes

Cada consulta principal debe comenzar con `SELECT` o `WITH`. Para una condición como `WHERE ClienteId = @ClienteId`, registra en el reporte un parámetro llamado `ClienteId`, sin incluir el signo `@` en su configuración.

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
- **Reporte destino:** el reporte que mostrará el detalle.
- **Parámetros enviados desde la fila:** agrega hasta 20 relaciones por enlace, por ejemplo `ClienteId → ClienteId` y `DocumentoId → DocumentoId`. La columna izquierda debe existir en el resultado padre y el parámetro derecho en el reporte hijo.
- **Ejecución automática:** abre el detalle ya consultado o solamente deja su filtro precargado.
- **Nueva pestaña:** conserva abierto el reporte principal mientras se consulta el detalle.
- **Heredar filtros:** envía al destino los valores actuales de todos los parámetros que existan con el mismo nombre en ambos reportes. Los mapeos de la fila tienen prioridad si apuntan al mismo parámetro. Si el hijo sólo necesita filtros como `FechaCorte`, puedes dejar la lista de mapeos vacía y activar esta opción.

El enlace sólo aparece cuando todas las columnas configuradas existen en el resultado, el reporte destino está activo y el usuario tiene permiso para ejecutarlo. Los enlaces de desglose y todos sus mapeos también viajan en los archivos JSON; al importar se vuelven a vincular por el nombre del reporte destino. Las configuraciones antiguas de un solo parámetro se migran automáticamente.

### Encabezado de Excel

Cada archivo exportado incluye antes de los KPI y de la tabla:

- Nombre de la empresa y del reporte.
- Base de datos, fecha/hora de generación y usuario.
- Parámetros aplicados con sus etiquetas; para parámetros de consulta se exporta el texto visible y no solamente el identificador.

Configura el nombre de la empresa en **Administración → Conexiones**. Las conexiones anteriores siguen siendo compatibles: si el campo queda vacío se utiliza el nombre de la conexión.

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
