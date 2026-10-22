# Imprenta Collantes · sistema de gestión

Aplicación ASP.NET Core 8 para administrar clientes, proveedores, productos y cotizaciones con líneas e importes calculados. El SRS de `Documentacion/REQUERIMIENTOS_SISTEMA.md` describe también capacidades objetivo que todavía no están implementadas, como el flujo de pedidos, BI e IA.

## Ejecutar el entorno local configurado

Se requiere el runtime ASP.NET Core 8 y una instancia SQL Server. La configuración local del proyecto apunta a `Server=.;Database=imprentacollantesbd`; no contiene contraseñas y supone que la instancia predeterminada de SQL Server está instalada y ejecutándose. Desde Visual Studio Code puedes iniciar el perfil `http` con la extensión C#; también puedes usar desde la raíz:

```sh
dotnet run --project ProyectoFinal/ProyectoFinal.csproj --launch-profile http
```

Abre `http://localhost:5092`. La base actual ya contiene `admin@admin.com` con el rol `Admin`; el secreto de bootstrap no está configurado ni se necesita para iniciar. La contraseña antigua por defecto debe cambiarse antes de usar el sistema con información sensible. Nunca guardes contraseñas en `appsettings.json`.

**Base actual (`imprentacollantesbd`):** se respaldó antes de los cambios en `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\imprentacollantesbd_pre_migration_20261022.bak` y se verificó con `RESTORE VERIFYONLY`. Como las tablas comerciales ya existían, se agregó su baseline `20261022042127_AddBusinessTables` al historial luego de comprobar el esquema y crear los 10 índices FK faltantes. Después se aplicó `20261022190105_WidenIdentityKeyColumns` para ampliar las claves de Identity. `AddBusinessTables` crea las tablas comerciales solo en una base nueva. No ejecutes migraciones automáticamente al arrancar.

**Otra base de datos existente:** no ejecutes `dotnet ef database update` hasta verificar su esquema, respaldarla y conciliar las filas de `__EFMigrationsHistory`. Una base con tablas comerciales ya creadas necesita baseline, no la migración `AddBusinessTables` ejecutada de nuevo.

## Límites actuales

- La cuenta registrada públicamente no tiene acceso a los CRUD hasta que un administrador le asigne el rol `Admin` desde «Usuarios y roles». No se asigna ningún rol automáticamente.
- Las cotizaciones admiten productos, cantidades y precios por línea. El servidor calcula subtotal, IGV del 18% y total, y guarda el total junto con las líneas; todavía no hay un flujo operativo para convertirlas en pedidos.
- Existen entidades de pedido y detalle, pero no un módulo para crear, convertir, actualizar estados o gestionar producción e inventario.
- El envío de correos es simulado (`FakeEmailSender`); no hay envío real ni generación de PDF. La recuperación por correo tampoco está disponible.
- `dotnet build ProyectoFinal.sln` compila la aplicación y `dotnet test ProyectoFinal.sln` ejecuta pruebas de navegación, permisos, formularios y antifalsificación con una base EF en memoria. Las pruebas no reemplazan una verificación con SQL Server real.

## Planificación propuesta

El orden recomendado para las siguientes etapas es:

1. **Definir reglas operativas antes de programar:** acordar los estados y transiciones de cotización, pedido y producción; cuándo una cotización se convierte en pedido; qué ocurre al editar o cancelar; cuándo se descuenta o repone stock; cómo resolver ventas concurrentes; qué eventos se auditan; y qué permisos tendrán `Vendedor` y `Operador de Taller` frente a `Admin`.
2. **Implementar pedidos, taller e inventario:** habilitar la conversión desde una cotización, el seguimiento de pedidos y producción, las reglas de stock acordadas, auditoría y autorización por rol. Cubrir las transiciones y casos de concurrencia con pruebas.
3. **Construir BI sobre datos transaccionales reales:** definir primero qué significa «facturado» para los indicadores —por ejemplo, pedido creado, entregado o factura real— y acordar las métricas antes de presentar ingresos y reportes.
4. **Integrar IA como asistencia, no como bloqueo:** incorporar revisión humana, manejo de errores y una alternativa manual para que la operación continúe si la IA no está disponible.

PDF y envío real de correo no forman parte de esta secuencia priorizada; deberán planificarse como alcance separado si se requieren.
