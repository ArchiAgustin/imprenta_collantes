# Inventario: esquema y activación por conteo físico

## Estado operativo verificado (05/11/2026)

La migración `20261029042715_PhysicalInventoryActivation` se aplicó en `imprentacollantesbd` tras crear y verificar el respaldo `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\imprentacollantesbd_pre_inventario_activacion_20261028_233216.bak` (`COPY_ONLY`, `CHECKSUM`, `RESTORE VERIFYONLY`). Los dos artículos actuales tienen unidad base importada `UNIDAD (BIENES)` y presentación **Unidad**, factor **1**. El almacén principal está activo; se registraron los inventarios `INV1-00000001` (apertura) y `INV1-00000002` (conteo posterior), ambos con detalle para los dos artículos. Los saldos actuales del almacén son operativos. `Producto.Stock` conserva los valores heredados 18 y 26; no se copiaron ni se suman a los saldos por almacén.

El administrador puede registrar nuevos conteos desde **Inventario**. La apertura de un almacén nuevo exige incluir todos los artículos y registrar su cantidad física, incluso cero; los conteos posteriores pueden incluir solo los productos revisados y los omitidos conservan el saldo actual. Las cantidades se capturan en la unidad base. El sistema registra detalle, diferencias, kárdex y auditoría de forma transaccional. Entradas físicas posteriores no crean costos ni compras históricas. La venta descuenta el **artículo vendido al confirmar**, nunca materiales del taller al comenzar el trabajo.

**No aprobar con cantidades supuestas**: excluir del conteo disponible los artículos ya comprometidos para ventas anteriores. Reiniciar cualquier instancia antigua de la aplicación para que cargue el código nuevo antes de operar. Un disparador rechaza escrituras al stock heredado durante el conteo o tras activación, como defensa ante versiones antiguas; no debe suprimirse. La migración no se aplica automáticamente al arrancar.

## Fase de esquema previa (histórica)

La migración `20261029040305_InventorySchemaFoundation` fue aplicada en `imprentacollantesbd` después de crear y verificar el respaldo `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\imprentacollantesbd_pre_inventario_esquema_20261028_230507.bak` (`COPY_ONLY`, `CHECKSUM`, `RESTORE VERIFYONLY`). MySQL **no** se modificó.

Se importaron las **64 unidades** de `bmotors.mae_unidadmedida` a `UnidadMedida`, preservando los IDs, descripción, descripción corta, código SUNAT y estado. La copia es transaccional y verifica el número de filas. La nueva tabla `PresentacionProducto` toma el diseño de `bmotors.lkg_presentacionproducto` **sin copiar sus filas**: sus identificadores de producto corresponden a otro catálogo. Tampoco se importó el `factor` de `mae_unidadmedida` como conversión de productos; el factor relevante pertenece a cada presentación y representa cuántas **unidades base** contiene una presentación.

Tablas creadas:

- `UnidadMedida`: catálogo de referencia importado (64 filas).
- `PresentacionProducto`: producto, unidad de medida, descripción, factor positivo, código de barras opcional y estado. **Vacía** hasta revisar cada producto y su unidad base.
- `Almacen`: catálogo de almacenes, **vacío**.
- `Stock`: clave `(almacén, producto)`, cantidad no negativa en unidad base (`decimal(18,3)`) y `rowVersion` para concurrencia. **Vacía y no operativa**.
- `CorteInventario` y `CorteInventarioDetalle`: cabecera de conteo y cantidades contadas/sistema en unidad base; **vacías**.
- `Kardex`: fecha, almacén, producto, tipo, cantidad, saldos antes/después y referencia; **vacía**. Todavía no registra compras, ventas ni ajustes.

En esta fase histórica se añadió `Producto.idUnidadMedidaBase` opcional, sin asignar entonces una unidad ficticia a los artículos. Sus precios, costos y stock heredados no cambiaron. El estado operativo y la gestión actual de unidades y presentaciones se describen al inicio de este documento.

### Importar el catálogo en otra instalación

Primero concilia las migraciones de su base SQL Server existente y haz/verifica un respaldo; nunca apliques migraciones automáticamente al iniciar. Una vez aplicada la migración nueva, desde la raíz del repositorio:

```powershell
.\scripts\Import-BmotorsUnidadesMedida.ps1 -MySqlExe 'C:\laragon\bin\mysql\mysql-8.0.30-winx64\bin\mysql.exe'
```

El script exige `UnidadMedida` vacía y falla sin modificarla si ya contiene filas; en una instalación nueva hay que usarlo **antes** de aplicar `PhysicalInventoryActivation`, que crea al menos la unidad base acordada. Acepta `-MySqlHost`, `-MySqlPort`, `-MySqlUser` y `-SqlConnectionString`; si se necesita contraseña, usa `-MySqlDefaultsFile` con un archivo de opciones MySQL privado **fuera del repositorio**. No compartas contraseñas por chat ni como argumentos de comando. Si la fuente cambia después de importar, no ejecutes de nuevo el script sobre un destino poblado: primero reconcilia sus registros.

## Unidades y presentaciones

La unidad base se selecciona explícitamente al crear un artículo; servicios no requieren unidad ni llevan stock. La unidad base de artículos con documentos o movimientos históricos no se puede cambiar desde el editor, para evitar reinterpretar cantidades ya registradas. Administración de productos permite crear y activar/desactivar presentaciones con un factor positivo de hasta tres decimales; cada factor representa cuántas unidades base contiene la presentación. Cotizaciones guardan el factor y los nombres utilizados; al confirmar o cancelar, las cantidades se convierten a unidades base para mover stock y Kárdex. Cambios posteriores del catálogo no reescriben documentos históricos.

No repetir la apertura del almacén principal ni copiar a saldos actuales el valor heredado de `Producto.Stock`. Antes de habilitar otro almacén, revisar cada unidad base y capturar el conteo físico completo de ese almacén.
