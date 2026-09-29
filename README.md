# Arx Reportes

Aplicación web ASP.NET Core 9 para publicar consultas de SQL Server como reportes parametrizados, mostrarlos en una tabla HTML y exportarlos a Excel.

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

## Transferir reportes

- Usa **Guía JSON para IA** para descargar el contrato completo, todos los tipos de parámetros y un ejemplo. Puedes entregarlo a una IA para que genere archivos compatibles con ARX Reportes.
- En **Administrar → Reportes**, usa **JSON** para descargar individualmente la definición de un reporte.
- El nombre del archivo incluye fecha, hora y segundos para evitar reemplazos accidentales.
- Usa **Importar JSON** para subirlo en esta u otra instalación, ya sea seleccionando un archivo o pegando directamente el texto JSON. Puedes conservar la conexión indicada en el contenido o elegir otra conexión destino.
- El reporte importado obtiene un identificador nuevo y una marca de fecha/hora en su nombre. Los archivos nunca incluyen usuarios, contraseñas ni cadenas de conexión.

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
