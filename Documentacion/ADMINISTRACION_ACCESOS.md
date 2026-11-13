# Administración de usuarios, roles y menús

## Gestión de cuentas

- Solo las cuentas con rol activo `Admin` pueden abrir `/Admin` y crear usuarios en `/Admin/CrearUsuario`. El registro público de Identity está protegido y ya no aparece como opción en la pantalla de inicio de sesión.
- Una cuenta se crea sin roles. El administrador asigna después los roles en `/Admin/AssignRole?userId={id}`; sin rol activo no aparecen módulos ni se autorizan sus rutas.
- La contraseña inicial se introduce en el formulario y no se envía por correo. La cuenta nace sin roles y con el correo pendiente de confirmación; tras confirmar podrá usar la recuperación de contraseña. El administrador puede reenviar el enlace desde la lista de usuarios.
- El correo saliente se configura en Administración → Configuración → Parámetros (`/Parametros`). Completa servidor SMTP, puerto, dirección y nombre remitente, y usuario/contraseña si el proveedor los exige. La contraseña se cifra con ASP.NET Core Data Protection y se puede enviar un correo de prueba a la cuenta Admin activa. Sin SMTP válido, confirmación y recuperación no podrán entregar mensajes.
- Las claves de ASP.NET Core Data Protection persisten fuera del repositorio: `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` en Windows (protegidas con DPAPI del usuario actual) y `~/.aspnet/DataProtection-Keys` en Unix. En despliegues, define `DataProtection:KeysPath` a almacenamiento persistente y restringido. En Unix producción añade un certificado de protección por `DataProtection:CertificatePath` y `DataProtection:CertificatePassword` desde un secret manager. No elimines ni regeneres el anillo: invalidaría cookies y enlaces de recuperación/confirmación aún vigentes.
- Desde la lista de usuarios se puede desactivar/reactivar una cuenta. La desactivación usa el bloqueo persistente de Identity, invalida el sello de seguridad y las solicitudes siguientes terminan la sesión. No se puede desactivar la cuenta propia ni dejar al sistema sin un `Admin` activo.

## Rol activo por sesión

Una cuenta puede pertenecer a varios roles. Al inicio de sesión se selecciona uno por defecto; si hay más de uno, el panel muestra **Rol activo**. Cambiarlo guarda el ID del rol en la sesión y las páginas, menú lateral, permisos por operación y restricciones `[Authorize(Roles=...)]` usan únicamente ese rol. El servidor vuelve a validar que el usuario siga perteneciendo a él en cada petición. Si se revoca o elimina, se selecciona otro rol disponible o ninguno.

El rol `Admin` está protegido y conserva acceso administrativo total cuando es el rol activo. Tener un rol adicional no mezcla sus concesiones: quien cambia a un rol limitado deja de tener las facultades de `Admin` para esa sesión.

## Árbol de menús

- En Administración → Menús (`/Menus`) se administran agrupadores y pantallas en un árbol de hasta tres niveles. Se puede reordenar, editar nombre/icono, mover y activar/desactivar. No se borra físicamente un menú con historia.
- El código técnico es único y estable. El destino se elige de una lista fija de pantallas implementadas; el formulario no acepta URL, controller ni action arbitrarios. Un agrupador sin destino solo organiza elementos y no hereda ni concede permisos.
- El destino de una pantalla existente no se cambia, para no reinterpretar asignaciones históricas. Se crea otra entrada de menú si se requiere un destino diferente. No se permiten ciclos, más de tres niveles, padres inactivos ni desactivar un agrupador mientras conserva descendientes activos.
- La navegación muestra únicamente destinos registrados y concedidos al rol activo. Si un padre o ancestro está inactivo, tampoco se muestran ni autorizan sus descendientes.

## Accesos por rol

En Administración → Permisos de menú (`/Permisos`) se configura por rol cada pantalla navegable con las operaciones `Ver`, `Crear`, `Editar`, `Eliminar` y `Procesar`. Los grupos se muestran como contexto jerárquico; marcar o autorizar una pantalla no concede sus hermanas ni sus descendientes. La autorización se comprueba en el servidor para las peticiones directas, no solo ocultando enlaces.

La matriz inicial es de mínimo privilegio: `Admin` tiene acceso total; `Vendedor` puede consultar clientes, productos, cotizaciones, ventas y taller, crear/editar clientes y cotizaciones, y procesar ventas; `Operador de Taller` solo puede ver el módulo Taller y procesar el avance de trabajos. Ningún rol operativo recibe eliminación inicial. Admin puede ajustar cada concesión desde la matriz.

Los roles actuales continúan en ASP.NET Identity (`AspNetRoles`, `AspNetUserRoles`) y las concesiones existentes en `PermisoRolMenu`. No se migran a tablas de `facturuna-shell` ni se importan usuarios o roles de ese proyecto.

## Migración y operación

La migración `20261103090000_HierarchicalMenusAndActiveRoles` agrega `idMenuPadre`, `icono` y `destino` a `MenuSistema`, admite controller nulo para agrupadores, agrega la FK jerárquica y la unicidad del destino. Conserva los seis IDs de módulos existentes, los ubica bajo Comercial/Producción, agrega Administración y destinos registrados, crea la opción de pre-cotización y copia a esa pantalla los permisos del módulo de cotizaciones.

La base local `imprentacollantesbd` registra `20261105100000_UnifiedSupplierIdentity`, `20261105120000_EmailConfigurationParametersMenu` y `20261106100000_QuoteLinePresentationSnapshots` como aplicadas. Para las presentaciones se creó y verificó el respaldo `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\imprentacollantesbd_pre_quote_presentations_20261106.bak`. La tabla SMTP sigue vacía hasta configurarla desde Parámetros. No habilitar auto-migraciones al iniciar.
