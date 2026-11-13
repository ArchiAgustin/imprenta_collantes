### 1. Estado de las historias de usuario

Los porcentajes son una estimación del alcance implementado, no una métrica automática. “TERMINADO” se refiere al flujo implementado de punta a punta en el código y pruebas disponibles. Actualizado el 03/11/2026 tras aplicar el árbol de menús y el módulo de catálogos SUNAT en la base local.

| HU | Historia | Estado | % avance | Evidencia en el código | Ruta/URL de la pantalla |
|---|---|---:|---:|---|---|
| HU01 | Iniciar sesión | TERMINADO | 100 % | `Areas/Identity/Pages/Account/Login.cshtml(.cs)`, `Program.cs` (Identity y autenticación), `ProyectoFinal.Tests/NavigationTests.cs` | `/Identity/Account/Login` |
| HU02 | Gestionar usuarios y roles (Admin, Vendedor, Operador) | TERMINADO | 100 % | `Controllers/AdminController.cs`, `Controllers/RolesController.cs`, `Controllers/PermisosController.cs`, `Services/ActiveRoleMiddleware.cs`; creación administrativa, asignación, rol activo por sesión y permisos jerárquicos. | `/Admin`, `/Admin/CrearUsuario`, `/Admin/AssignRole?userId={id}`, `/Roles`, `/Permisos` |
| HU03 | Consultar bitácora de auditoría | EN PROGRESO | 75 % | `Controllers/AuditoriaController.cs`, `Views/Auditoria/Index.cshtml`, `Models/AuditoriaOperacion.cs`, `Services/AuditoriaFactory.cs` | `/Auditoria` |
| HU04 | Registrar clientes con validación de DNI/RUC | EN PROGRESO | 85 % | `Controllers/ClientesController.cs`, `Services/IdentificacionService.cs`, `Models/DocumentoIdentidadTipo.cs`; valida longitudes, unicidad y ubigeo. No se encontró consulta de autenticidad a RENIEC/SUNAT. | `/Clientes/Create` |
| HU05 | Ver historial del cliente (cotizaciones y pedidos) | NO INICIADO | 0 % | `Views/Clientes/Details.cshtml` muestra datos del cliente, no su historial comercial. | `/Clientes/Details/{id}` (detalle; historial NO ENCONTRADO) |
| HU06 | Registrar proveedores | TERMINADO | 100 % | `Controllers/ProveedorsController.cs`, vistas `Views/Proveedors/*`, `Services/IdentificacionService.cs` | `/Proveedors/Create` |
| HU07 | Gestionar catálogo de productos e insumos con stock | TERMINADO | 95 % | `Controllers/ProductoesController.cs`, `Controllers/InventarioController.cs`, `Services/InventarioService.cs`, `Models/Producto.cs`, `Models/StockAlmacen.cs` | `/Productoes`, `/Inventario` |
| HU08 | Crear cotización con cálculo automático de IGV 18 % | TERMINADO | 100 % | `Controllers/CotizacionsController.cs`, `Models/CotizacionFormViewModel.cs`, `Views/Cotizacions/Create.cshtml`; el servidor recalcula subtotal, IGV y total. | `/Cotizacions/Create` |
| HU09 | Prellenar cotización con IA a partir de texto | EN PROGRESO | 75 % | `Controllers/PreCotizacionController.cs`, `Services/GroqSolicitudExtractor.cs`, `Services/ProteccionSolicitudService.cs`; requiere `Groq:ApiKey` y conectividad externa para una llamada real. | `/PreCotizacion` |
| HU10 | Convertir cotización en pedido con descuento de stock transaccional | TERMINADO | 100 % | `Controllers/VentasController.cs`, `Services/VentaService.cs`; confirma cotización, crea venta y pedido, descuenta stock y registra kárdex dentro de la transacción. | Desde `/Cotizacions/Details/{id}`; ventas en `/Ventas` |
| HU11 | Registrar entradas de insumos y consultar kárdex | TERMINADO | 95 % | `Controllers/InventarioController.cs`, `Services/InventarioService.cs`, `Models/Kardex.cs`; entrada física y movimientos de inventario. No representa compras ni costos de compra. | `/Inventario` |
| HU12 | Ver alertas de stock mínimo | TERMINADO | 100 % | `Services/AnalyticsService.cs`, `Views/Analytics/Index.cshtml`; umbral configurable en pantalla y alertas de artículos no servicios. | `/Analytics?umbralStock=5` |
| HU13 | Ver tablero de producción por estados | TERMINADO | 100 % | `Controllers/TallerController.cs`, `Services/VentaService.cs`, `Models/EstadosPedido.cs`, `Views/Taller/Index.cshtml`; flujo En cola → En impresión/acabado → Listo para recojo → Entregado. | `/Taller` |
| HU14 | Dashboard BI: ventas, conversión, stock y entregas | TERMINADO | 95 % | `Controllers/AnalyticsController.cs`, `Services/AnalyticsService.cs`, `Models/AnalyticsDashboardViewModel.cs`, `Views/Analytics/Index.cshtml`; incluye ventas, conversión, stock, estados de taller y costos conocidos. | `/Analytics` |
| HU15 | Exportar reportes a Excel o PDF | NO INICIADO | 0 % | Exportadores, acciones o librerías de Excel/PDF: NO ENCONTRADO. | NO ENCONTRADO |
| HU16 | Generar cotización en PDF | EN PROGRESO | 60 % | `Controllers/CotizacionsController.cs`, `Views/Cotizacions/Imprimir.cshtml`, `wwwroot/css/print-document.css`; existe vista A4 imprimible desde el navegador, no generación/descarga de PDF desde el servidor. | `/Cotizacions/Imprimir/{id}` |
| HU17 | Enviar cotización por correo | NO INICIADO | 0 % | `Services/FakeEmailSender.cs` solo escribe en consola; envío real de cotizaciones por email: NO ENCONTRADO. | NO ENCONTRADO |
| HU18 | Revalidar cotizaciones vencidas conservando la original | TERMINADO | 100 % | `Controllers/CotizacionsController.cs`, `Services/CotizacionExpirationService.cs`; genera una cotización nueva enlazada tras revisión, con vigencia predeterminada de 7 días. | `/Cotizacions/Details/{id}` → “Revisar y emitir nueva” |
| HU19 | Configurar series y correlativos de cotizaciones y ventas internas | TERMINADO | 100 % | `Controllers/SeriesInternasController.cs`, `Services/SerieDocumentoInternoService.cs`, `Models/SerieDocumentoInterno.cs`; migración `20261030194836_ConfigurableInternalSeries`. La configuración local registrada fue C001 y V001 desde 4532. | `/SeriesInternas` |
| HU20 | Imprimir documentos comerciales internos A4 | TERMINADO | 100 % | `Views/Cotizacions/Imprimir.cshtml`, `Views/Ventas/Imprimir.cshtml`, `wwwroot/css/print-document.css`. Indican que no son comprobantes tributarios ni emitidos a SUNAT. | `/Cotizacions/Imprimir/{id}`, `/Ventas/Imprimir/{id}` |
| HU21 | Crear, asignar y desactivar usuarios desde administración | TERMINADO | 100 % | `Controllers/AdminController.cs`, `Views/Admin/CrearUsuario.cshtml`, `Services/ActiveRoleMiddleware.cs`; registro público bloqueado, usuarios nuevos sin roles y sesiones invalidadas al desactivar. | `/Admin`, `/Admin/CrearUsuario` |
| HU22 | Administrar menús con hijos/subhijos y accesos independientes por rol | TERMINADO | 100 % | `Controllers/MenusController.cs`, `Views/Menus/*`, `Controllers/PermisosController.cs`, `Services/MenuAccessService.cs`; máximo tres niveles, destinos allow-listed, validación de ciclos y no herencia de permisos. Migración aplicada localmente. | `/Menus`, `/Menus/Create`, `/Menus/Edit/{id}`, `/Permisos/Editar/{roleId}` |
| HU23 | Consultar catálogos SUNAT con gestión acotada | TERMINADO | 100 % | `Controllers/CatalogosSunatController.cs`, `Services/CatalogoSunatService.cs`, `Services/CatalogoSunatRegistry.cs`, `Models/CatalogosSunat.cs`; 14 tablas fuente importadas, paginación, permisos Ver/Procesar y auditoría. No emite CPE. | `/CatalogosSunat` y sus 17 submenús |

### 2. Pantallas existentes

Se enumeran las vistas navegables encontradas bajo `Views/` y las páginas Razor de Identity. “Implementada” describe la presencia de vista y acción; no equivale a una comprobación HTTP en vivo hoy. Los módulos que consultan datos necesitan SQL Server y sus rutas pueden pedir inicio de sesión o rol Admin.

| Módulo | Pantallas y rutas | Estado comprobable |
|---|---|---|
| Inicio y errores | `/` o `/Home/Index`, `/Home/Privacy`, `/Home/Error` | Vistas presentes; `/Home/Error` es la pantalla de error. El acceso live a datos no se pudo probar. |
| Panel | `/Identity/Usuario/Panel` | Razor Page presente; requiere sesión. |
| Acceso / Identity | `/Identity/Account/Login`, `/Register`, `/Logout`, `/AccessDenied`, `/Lockout`, `/ForgotPassword`, `/ForgotPasswordConfirmation`, `/ResetPassword`, `/ResetPasswordConfirmation`, `/RegisterConfirmation`, `/ResendEmailConfirmation`, `/ConfirmEmail`, `/ConfirmEmailChange`, `/ExternalLogin`, `/LoginWith2fa`, `/LoginWithRecoveryCode` | Páginas incluidas por Identity y sus vistas locales. Envío real de correo no configurado. |
| Administración de cuenta Identity | `/Identity/Account/Manage/Index`, `/Email`, `/ChangePassword`, `/SetPassword`, `/ExternalLogins`, `/TwoFactorAuthentication`, `/EnableAuthenticator`, `/Disable2fa`, `/ResetAuthenticator`, `/ShowRecoveryCodes`, `/GenerateRecoveryCodes`, `/PersonalData`, `/DownloadPersonalData`, `/DeletePersonalData` | Páginas Identity presentes; requieren sesión. |
| Roles heredados de Identity | `/Identity/Account/Manage/IndexRole`, `/CreateRole`, `/EditRole`, `/AsignarRoles` | Archivos Razor presentes; algunos handlers redirigen al módulo MVC `/Roles`. No son el flujo administrativo principal. |
| Usuarios | `/Admin`, `/Admin/CrearUsuario`, `/Admin/AssignRole?userId={id}` | Vistas presentes; Admin activo requerido. Nuevas cuentas nacen sin roles. `/Identity/Account/Register` exige Admin, no es registro público. |
| Roles | `/Roles`, `/Roles/Create`, `/Roles/Edit/{id}` | Vistas presentes; Admin requerido. El borrado es acción POST desde la lista, no una página separada. |
| Permisos por rol | `/Permisos`, `/Permisos/Editar/{roleId}` | Vistas presentes; Admin requerido. |
| Auditoría | `/Auditoria` (filtros opcionales `tipoEntidad` e `idEntidad`) | Vista presente; Admin requerido; lista como máximo 500 eventos. |
| Clientes | `/Clientes`, `/Clientes/Create`, `/Clientes/Details/{id}`, `/Clientes/Edit/{id}`, `/Clientes/Delete/{id}` | CRUD presente; requiere sesión y permisos del módulo. El detalle no incluye historial de cotizaciones/pedidos. |
| Proveedores | `/Proveedors`, `/Proveedors/Create`, `/Proveedors/Details/{id}`, `/Proveedors/Edit/{id}`, `/Proveedors/Delete/{id}` | CRUD presente; requiere sesión y permisos del módulo. |
| Productos | `/Productoes`, `/Productoes/Create`, `/Productoes/Details/{id}`, `/Productoes/Edit/{id}`, `/Productoes/Delete/{id}` | CRUD presente; requiere sesión y permisos del módulo. |
| Cotizaciones | `/Cotizacions`, `/Cotizacions/Create`, `/Cotizacions/Details/{id}`, `/Cotizacions/Edit/{id}`, `/Cotizacions/Delete/{id}`, `/Cotizacions/Imprimir/{id}`, `/Cotizacions/Revalidar/{id}` | Vistas y acciones presentes; requieren sesión/permisos. Revalidar renderiza el formulario de creación. |
| Pre-cotización con IA | `/PreCotizacion` | Una vista con etapas POST `Preparar`, `Analizar` y `Cargar`; requiere sesión, API key y servicio externo para análisis real. |
| Ventas | `/Ventas`, `/Ventas/Details/{id}`, `/Ventas/Imprimir/{id}` | Vistas presentes; requiere sesión/permisos. Confirmar y cancelar son acciones POST, no pantallas. |
| Inventario | `/Inventario`, `/Inventario/Conteo/{id}` | Vistas presentes; Admin requerido. `Iniciar`, `Guardar`, `Aprobar`, `Anular` y `Entrada` son acciones POST. |
| Taller | `/Taller`, `/Taller/Details/{id}` | Vistas presentes; requiere sesión/permisos. Cambio de estado es POST. |
| Analítica | `/Analytics` | Vista presente; Admin requerido. |
| Series internas | `/SeriesInternas`, `/SeriesInternas/Create`, `/SeriesInternas/Edit/{id}` | Vistas presentes; Admin requerido. Crear/editar comparten `Editar.cshtml`. |
| Menús del sistema | `/Menus`, `/Menus/Create`, `/Menus/Edit/{id}` | CRUD jerárquico presente; Admin requerido. Solo enlaza destinos registrados por la aplicación. La migración está aplicada localmente. |
| Catálogos SUNAT | `/CatalogosSunat`, `/CatalogosSunat/{acción}` | Consulta paginada por submenú; requiere permiso Ver. Activar/desactivar requiere Procesar. Cambios auditados. No genera ni envía comprobantes. |
| Selección de rol | POST `/RoleSelection/Cambiar` | Acción sin vista propia; guarda el rol activo en sesión y valida pertenencia desde el servidor. |
| Ubigeo | `/Ubigeos` (consulta usada por formularios) | Endpoint JSON, no tiene vista propia. |

Las vistas parciales y layouts (`Views/Shared/*`, `_PanelLayout.cshtml`, `_PrintLayout.cshtml`, parciales de formularios y validación) son componentes reutilizados, no pantallas independientes. No se hizo un recorrido visual de todas las rutas en navegador; los flujos nuevos se probaron con HTTP y SQL Server temporal.

### 3. Historial de trabajo según Git

Salida de `git log --pretty=format:"%h|%an|%ad|%s" --date=short`:

```text
1df37cf|Archi|2026-10-22|correcciones previo desarrollo de requerimientos
b24f03c|Archi|2026-10-22|inicio
```

| Semana | Autor | Commits | Primero–último | Módulos tocados según commits |
|---|---|---:|---|---|
| Semana 0: antes del 21/10/2026 | — | 0 | — | — |
| Semana 1: 21–27/10/2026 | Archi | 2 | 2026-10-22 – 2026-10-22 | Inicio del proyecto MVC/Identity, CRUD de clientes/productos/proveedores/cotizaciones, Identity/roles, migraciones, pruebas, documentación y estilos; el segundo commit corrige y amplía administración de roles y flujo de cotizaciones. |
| Semana 2: 28/10–03/11/2026 | — | 0 | — | — |
| Semana 3: 04/11–10/11/2026 | — | 0 | — | — |

**Importante:** el árbol de trabajo al revisar contiene numerosos cambios staged, unstaged y archivos sin seguimiento. Esos cambios incluyen ventas, taller, inventario, auditoría, analítica, IA, series configurables y documentación; **no forman parte de los dos commits listados**. Git no permite atribuirlos a un autor o fecha de commit.

Hay artefactos con fecha `20250508014140_InitialCreate` y modelos/contextos scaffold heredados (`Models/AspNetUser.cs`, `Models/DbContextNombre.cs`, `Areas/Identity/Data/ProyectoFinalContext.cs`). Su fecha/nombre es evidencia de artefactos previos, pero que provengan de un proyecto de Imprenta Collantes de 2025: **NO ENCONTRADO**. Los contextos/modelos señalados como legacy no son el contexto EF registrado en `Program.cs`.

### 4. Base de datos

El único contexto registrado es `Data/ApplicationDbContext.cs`; usa SQL Server según `Program.cs` y `appsettings.json`. Los DbSet de catálogos SUNAT incorporados son `TiposDocumentoPagoSunat`, `MonedasSunat`, `DetraccionesSunat`, `TiposMedioPagoSunat`, `MotivosNotaCreditoSunat`, `MotivosNotaDebitoSunat`, `MotivosTrasladoSunat`, `TiposEntidadFinancieraSunat`, `EntidadesFinancierasSunat`, `TiposActividadSunat`, `TiposContribuyenteSunat`, `TiposViaSunat`, `TiposZonaSunat` y `EstadosEnvioSunat`. Se reutilizan `DocumentoIdentidadTipos`, `UnidadesMedida` y `Ubigeos` existentes.

| Migración del repositorio | Fecha del ID | Alcance resumido |
|---|---|---|
| `00000000000000_CreateIdentitySchema` | Baseline | Esquema Identity. |
| `20250508014140_InitialCreate` | 2025-05-08 | Esquema inicial legado. |
| `20261022042127_AddBusinessTables` | 2026-10-22 | Tablas comerciales iniciales. |
| `20261022190105_WidenIdentityKeyColumns` | 2026-10-22 | Ampliación de columnas de claves Identity. |
| `20261022214724_VentasTallerYPermisos` | 2026-10-22 | Ventas, taller, auditoría/permisos. |
| `20261023135013_StockConcurrency` | 2026-10-23 | Concurrencia de stock. |
| `20261024041212_AddHistoricalCosts` | 2026-10-24 | Costos históricos. |
| `20261029031316_CustomerSupplierCatalogs` | 2026-10-29 | Catálogos de documentos, ubicación y productos. |
| `20261029040305_InventorySchemaFoundation` | 2026-10-29 | Estructura de almacenes, saldos, cortes y kárdex. |
| `20261029042715_PhysicalInventoryActivation` | 2026-10-29 | Activación de inventario físico. |
| `20261030191601_InternalCommercialDocuments` | 2026-10-30 | Documentos comerciales internos y snapshots de venta. |
| `20261030192147_QuotationPrintSnapshots` | 2026-10-30 | Snapshots de impresión de cotizaciones. |
| `20261030194836_ConfigurableInternalSeries` | 2026-10-30 | Series configurables y ancho de correlativo. |
| `20261103090000_HierarchicalMenusAndActiveRoles` | 2026-11-03 | Árbol de menús, destinos seguros, agrupación de módulos y permiso independiente para pre-cotización. |
| `20261103110000_SunatReferenceCatalogs` | 2026-11-03 | 14 tablas de catálogos importados y menús Catálogos SUNAT. |

La base local `imprentacollantesbd` registra como última migración `20261103110000_SunatReferenceCatalogs`. Antes de aplicarla se creó y verificó `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\imprentacollantesbd_pre_sunat_catalogs_20261103_225846.bak`. Se importaron 885 filas desde la copia local MySQL `kipu`; se conciliaron sin duplicar los 6 tipos de documento, 64 unidades y 2 088 ubigeos existentes. Los recuentos y la relación de las 82 entidades financieras con sus 9 tipos se verificaron después.

### 5. Pendientes y problemas conocidos

- **Pruebas:** `dotnet test ProyectoFinal.Tests/ProyectoFinal.Tests.csproj` compiló y pasó **39/39 pruebas**, incluyendo tests opt-in con SQL Server real que migran una base temporal y prueban flujos concurrentes y permisos SUNAT.
- Las pruebas SQL Server son opt-in mediante `PROYECTOFINAL_SQL_TEST_CONNECTION`; se ejecutaron con una conexión local y aprobaron los flujos SQL en bases temporales. En `imprentacollantesbd` se verificaron la última migración y los recuentos importados.
- La IA requiere configurar `Groq:ApiKey` (por secret o variable de configuración) y conectividad con Groq. No se confirmó una llamada real al proveedor en esta revisión. Hay pruebas con extractor simulado.
- El correo está sustituido por `FakeEmailSender`; envío real de cotizaciones: **NO ENCONTRADO**.
- El módulo de catálogos es de consulta y activación acotada; altas/ediciones de códigos normativos se hacen mediante una nueva importación revisada, no con CRUD libre.
- No se encontró historial comercial en la ficha del cliente, exportación Excel/PDF ni generador PDF de servidor. La impresión actual es HTML con estilos de impresión del navegador.
- DNI/RUC se validan por tipo, cantidad de dígitos, unicidad y datos requeridos; verificación de validez oficial contra RENIEC/SUNAT: **NO ENCONTRADO**.
- Hay código `NotSupportedException` en ramas de Identity predeterminado para almacenamiento de email (`Register.cshtml.cs`, `ExternalLogin.cshtml.cs`); se registra un sender simulado, no un proveedor de correo.
- Búsqueda de `TODO`/`FIXME` en código propio: no encontró pendientes explícitos. Los TODO encontrados pertenecen a jQuery de terceros. Las funciones listadas como incompletas arriba son brechas respecto a las historias, no necesariamente TODOs comentados.
- La selección de rol utiliza sesión distribuida en memoria; la configuración está orientada a una sola instancia web. Persistencia/compartición de sesiones entre múltiples servidores: **NO ENCONTRADO**.
- El alcance de cotizaciones/ventas permanece **interno**; la integración tributaria SUNAT no está implementada ni debe inferirse de los números internos.

### 6. Tecnologías y versiones reales

| Tecnología | Evidencia / versión |
|---|---|
| .NET | Proyecto dirigido a `net8.0` (`ProyectoFinal.csproj`). SDK instalado al revisar: `10.0.402`; runtimes instalados incluyen .NET `8.0.32`. El SDK instalado no cambia el target del proyecto. |
| ASP.NET Core | Framework compartido .NET 8; paquetes Identity/UI `8.0.15`. |
| Entity Framework Core | Paquetes `Microsoft.EntityFrameworkCore.SqlServer` y `Microsoft.EntityFrameworkCore.Tools` `8.0.15`. |
| SQL Server | Proveedor EF Core SQL Server configurado y connection string local `Server=.;Database=imprentacollantesbd`. Versión del motor local: `16.0.1000.6` (SQL Server 2022). |
| Bootstrap | `5.1.0`, según encabezado de `wwwroot/lib/bootstrap/dist/css/bootstrap.css`. |
| jQuery | `3.6.0`, según encabezado de `wwwroot/lib/jquery/dist/jquery.js`. También están jQuery Validation y Unobtrusive Validation. |
| Chart.js | **NO ENCONTRADO** en código o `wwwroot/lib`; el dashboard dibuja barras con HTML/CSS propio. |
| PDF | Librería/paquete de generación PDF: **NO ENCONTRADO**. Hay impresión HTML/CSS para diálogo de impresión del navegador. |
| Excel | Librería/paquete de exportación Excel: **NO ENCONTRADO**. |
| JavaScript / package.json | `package.json`: **NO ENCONTRADO**; no se identifica un pipeline npm en el proyecto. Las librerías frontend están servidas desde `wwwroot/lib`. |
| IA | `HttpClient` propio a `https://api.groq.com/openai/v1/chat/completions`, modelo por defecto `openai/gpt-oss-20b` (`Services/GroqSolicitudExtractor.cs`). SDK dedicado de Groq: **NO ENCONTRADO**. |
