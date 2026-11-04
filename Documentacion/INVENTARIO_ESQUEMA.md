# Inventario: esquema y activación por conteo físico

## Estado operativo (28/10/2026)

La migración `20261029042715_PhysicalInventoryActivation` se aplicó en `imprentacollantesbd` tras crear y verificar el respaldo `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\imprentacollantesbd_pre_inventario_activacion_20261028_233216.bak` (`COPY_ONLY`, `CHECKSUM`, `RESTORE VERIFYONLY`). Creó **Almacén principal** y asignó a los dos artículos existentes la unidad base importada `UNIDAD (BIENES)` con presentación **Unidad**, factor **1**. `Producto.Stock` se mantuvo intacto (18 y 26); no se copiarán esos números como apertura. SQL Server muestra **cero** cortes, saldos y asientos de kárdex: el inventario nuevo **todavía no está activo**.

En la versión nueva, el administrador debe abrir **Inventario** en el panel, coordinar la pausa de ventas, iniciar un conteo, ingresar físicamente todas las cantidades (también ceros), guardarlas y revisarlas antes de aprobar. El conteo en borrador pausa ventas, cancelaciones, altas/cambios de artículos y entradas; anularlo reanuda operaciones sin alterar existencias. Aprobar la apertura escribe saldos y movimientos de apertura para cantidades positivas, marca la fecha de activación y hace que ventas, cancelaciones, analítica y productos usen `Stock` en lugar de `Producto.Stock`. Los ceros quedan documentados en el corte y su saldo cero, sin crear un movimiento de cantidad cero. Cortes posteriores registran únicamente las diferencias como ajustes. Entradas físicas posteriores requieren motivo y no crean costos ni compras históricas. La venta descuenta el **artículo vendido al confirmar**, nunca materiales del taller al comenzar el trabajo.

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

Se añadió `Producto.idUnidadMedidaBase` opcional, sin asignar una unidad ficticia a los dos artículos existentes. Sus precios, costos y stock anteriores **no cambiaron**. El editor actual conserva este campo aunque aún no permita configurarlo.

### Importar el catálogo en otra instalación

Primero concilia las migraciones de su base SQL Server existente y haz/verifica un respaldo; nunca apliques migraciones automáticamente al iniciar. Una vez aplicada la migración nueva, desde la raíz del repositorio:

```powershell
.\scripts\Import-BmotorsUnidadesMedida.ps1 -MySqlExe 'C:\laragon\bin\mysql\mysql-8.0.30-winx64\bin\mysql.exe'
```

El script exige `UnidadMedida` vacía y falla sin modificarla si ya contiene filas; en una instalación nueva hay que usarlo **antes** de aplicar `PhysicalInventoryActivation`, que crea al menos la unidad base acordada. Acepta `-MySqlHost`, `-MySqlPort`, `-MySqlUser` y `-SqlConnectionString`; si se necesita contraseña, usa `-MySqlDefaultsFile` con un archivo de opciones MySQL privado **fuera del repositorio**. No compartas contraseñas por chat ni como argumentos de comando. Si la fuente cambia después de importar, no ejecutes de nuevo el script sobre un destino poblado: primero reconcilia sus registros.

## Próximo paso

Realizar el conteo real en la aplicación y aprobarlo solamente después de comprobar que cada artículo tenga la cantidad física correcta. Antes de aprobar, la única fuente operativa sigue siendo `Producto.Stock`; nunca sumar ambas tablas ni asumir que los saldos nuevos están vigentes. El conteo debe ingresarlo el operador, no inferirse del dato heredado.
