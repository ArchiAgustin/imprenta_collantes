# Catálogos SUNAT importados desde kipuasistente

## Alcance

Los datos se leyeron de la **copia local** MySQL `kipu`; no se consultó la base remota ni se copiaron credenciales. Se importaron 14 tablas fuente con **885 filas** a nuevas tablas SQL Server, conservando IDs, códigos, descripciones y estado, con FK entre entidades financieras y su tipo. Los campos de auditoría del sistema PHP no se importaron porque sus usuarios e IDs pertenecen a otra aplicación.

| Tabla fuente | Filas | Menú de consulta |
|---|---:|---|
| `mae_tipodocumentopago` | 141 | Tipos de comprobante |
| `mae_moneda` | 2 | Monedas |
| `mae_detraccion` | 37 | Detracciones |
| `mae_tipomediopago` | 28 | Medios de pago |
| `mae_motivonc` | 13 | Motivos de nota de crédito |
| `mae_motivond` | 3 | Motivos de nota de débito |
| `mae_motivotraslado` | 14 | Motivos de traslado |
| `mae_entidadfinanciera_tipo` | 9 | Tipos de entidad financiera |
| `mae_entidadfinanciera` | 82 | Entidades financieras |
| `mae_tipoactividad` | 433 | Tipos de actividad económica |
| `mae_tipocontribuyente` | 51 | Tipos de contribuyente |
| `mae_via_tipo` | 32 | Tipos de vía |
| `mae_zona_tipo` | 32 | Tipos de zona |
| `ven_estadosunat` | 8 | Estados de envío SUNAT |

Se reutilizan, sin duplicar, los catálogos ya presentes en Collantes: `documentoidentidad_tipo` (6 tipos), `UnidadMedida` (64 unidades) y `ubigeo` (2 088 ubicaciones). Se comprobaron por código/descripción; se completó a 15 la longitud del documento tipo `A`, que estaba nula. El campo fuente `mae_unidadmedida.factor` se omite intencionalmente: no define conversiones de presentación de producto.

## Exclusiones solicitadas

- `mae_errorsunat` (errores SUNAT), `mae_pais`, `mae_nacionalidad` y `mae_motivoncp` (vacía).
- Catálogos laborales: `mae_estadocivil`, `mae_niveleducativo`, `mae_ocupacion`, `mae_puestotrabajo`, `mae_regimenlaboral` y `mae_vinculofamiliar`.
- Las tablas jerárquicas `sun_segmento`, `sun_familia`, `sun_clase` y `sun_producto` —incluidos sus 49 022 productos—, tal como se acordó.
- `cpe_sunat_validacion` y otras tablas de comprobantes, envíos o respuestas son datos operativos, no catálogos; no se importaron.

## Uso y permisos

El módulo está en `/CatalogosSunat`; sus submenús están bajo **Catálogos SUNAT** en el árbol de navegación. Cada listado incluye búsqueda y paginación. Los códigos y descripciones son de solo lectura: las únicas escrituras son activar/desactivar, con auditoría. No se puede desactivar una unidad usada por productos/presentaciones, un documento usado por clientes ni un tipo financiero con entidades dependientes. `ubigeos` es de consulta.

El rol activo necesita `Ver` en cada submenú; cambiar el estado requiere `Procesar`. `Admin` conserva su acceso administrativo. Esta función **no emite comprobantes, no envía CPE y no integra Collantes con los servicios SUNAT**.

## Migración e importación reproducible

La migración `20261103110000_SunatReferenceCatalogs` crea las 14 tablas nuevas y sus menús. Antes se realizó el respaldo:

`C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\imprentacollantesbd_pre_sunat_catalogs_20261103_225846.bak`

El respaldo pasó `RESTORE VERIFYONLY`. La migración se aplicó a `imprentacollantesbd`. Después se ejecutó `scripts/Import-KipuSunatCatalogs.ps1`, que lee solo la base local `kipu`, valida los recuentos esperados, concilia las tablas existentes y carga las nuevas en **una sola transacción SQL Server**. `-VerifyOnly` repite las validaciones sin confirmar escritura. Si las tablas destino ya tienen filas distintas a la fuente, el script se detiene y no las sobrescribe. No guarda contraseñas; si MySQL local las requiere, usa `-MySqlOptionsFile` apuntando a un archivo privado fuera del repositorio.

Con la copia local levantada, se puede repetir la verificación y/o la importación así:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Import-KipuSunatCatalogs.ps1 -VerifyOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Import-KipuSunatCatalogs.ps1
```

Para otra instalación MySQL local se pueden indicar `-MySqlExe`, `-MySqlHost`, `-MySqlPort`, `-MySqlDatabase` y `-MySqlUser`; nunca se toman credenciales de `kipuasistente/config/Config.php`.
