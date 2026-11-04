# Catálogos de ubigeo y tipo de documento

La aplicación opera con **SQL Server**; los catálogos originales están en MySQL `facturuna`. La migración EF `20261029031316_CustomerSupplierCatalogs` crea `dbo.ubigeo` y `dbo.documentoidentidad_tipo` (sin el prefijo `mae_`) y añade los campos opcionales al esquema de Cliente y Proveedor para conservar registros anteriores. **No** se ejecuta al arrancar el sistema. Los formularios sí exigen los nuevos datos al crear o editar.

## Copia desde MySQL

Primero haz y verifica un respaldo de la base SQL Server, revisa que la migración corresponda al esquema existente y aplícala. Desde la raíz del repositorio, importa los datos de MySQL mediante:

```powershell
.\scripts\Import-FacturunaCatalogos.ps1 -MySqlExe 'C:\laragon\bin\mysql\mysql-8.0.30-winx64\bin\mysql.exe'
```

El script lee las dos tablas fuente, conserva identificadores, códigos, jerarquía, textos Unicode y metadatos de documento. Solo importa en un **destino vacío** y realiza ambas copias y la clasificación de los DNI existentes en una **transacción**; si falla, revierte la importación. Verifica recuentos antes de confirmar. No modifica MySQL. Si el usuario de MySQL necesita contraseña, proporciónale al script `-MySqlDefaultsFile` con un archivo de opciones privado **fuera del repositorio** (no pases la contraseña como argumento ni la compartas por chat). Para otros servidores usa `-MySqlHost`, `-MySqlPort`, `-MySqlUser` y `-SqlConnectionString` desde una sesión privada; protege cualquier cadena que contenga credenciales. No vuelvas a ejecutar el script sobre tablas pobladas: revisa los datos y concilia los cambios antes de sincronizarlos.

## Resultado en el entorno local

Se creó y verificó el respaldo `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\imprentacollantesbd_pre_identidad_20261028_221820.bak` (`COPY_ONLY`, `CHECKSUM`, `RESTORE VERIFYONLY`). Se aplicó únicamente la migración nueva a `imprentacollantesbd`; se importaron **2 088 ubigeos** y **6 tipos de documento** (incluidos DNI código `1` y RUC código `6`). Se clasificaron los **2 clientes existentes** con DNI de 8 dígitos sin alterar sus nombres, direcciones ni relaciones. Los **2 proveedores antiguos** mantienen RUC, dirección y ubigeo vacíos hasta que se registren datos reales. No se creó ningún RUC/dirección/ubigeo inventado.

## Reglas de captura

- Cliente: escoger DNI (8 dígitos) o RUC (11 dígitos), ingresar número y nombre o razón social. Para RUC son obligatorios dirección y **distrito** del ubigeo; con DNI ambos son opcionales.
- Proveedor: nombre, RUC de 11 dígitos, dirección y distrito obligatorios; contacto opcional.
- No se permiten documentos duplicados entre clientes del mismo tipo ni RUC duplicados entre proveedores. Se permite que una misma entidad sea cliente y proveedor.
- El selector usa departamento → provincia → distrito; el servidor valida que el código corresponda a un distrito presente en el catálogo. Se valida longitud y formato del documento, **no** su condición tributaria ante SUNAT.
