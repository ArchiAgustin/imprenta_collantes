# Imprenta Collantes · sistema de gestión

Aplicación ASP.NET Core 8 para administrar clientes, proveedores, productos y registros de cotizaciones. Los pedidos, cálculos automáticos, reportes e IA descritos en `Documentacion/REQUERIMIENTOS_SISTEMA.md` aún no están implementados.

## Preparar un entorno nuevo

Necesitas .NET 8 SDK y SQL Server (en Windows también funciona SQL Server LocalDB). Desde la raíz del repositorio:

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=ProyectoFinal;Trusted_Connection=True;TrustServerCertificate=True;" --project ProyectoFinal/ProyectoFinal.csproj
dotnet user-secrets set "BootstrapAdmin:Email" "admin@ejemplo.com" --project ProyectoFinal/ProyectoFinal.csproj
dotnet user-secrets set "BootstrapAdmin:Password" "<contraseña-segura-distinta>" --project ProyectoFinal/ProyectoFinal.csproj
dotnet tool install --global dotnet-ef --version 8.0.15
dotnet ef database update --project ProyectoFinal/ProyectoFinal.csproj --startup-project ProyectoFinal/ProyectoFinal.csproj --context ApplicationDbContext
dotnet run --project ProyectoFinal/ProyectoFinal.csproj --launch-profile http
```

Abre `http://localhost:5092`. El administrador configurado se crea al iniciar (o recibe el rol Admin si ya existía). Cambia/elimina los valores de bootstrap de tus secretos después de aprovisionar la cuenta; nunca incluyas contraseñas ni conexiones personales en el repositorio. Puedes usar variables de entorno `ConnectionStrings__DefaultConnection`, `BootstrapAdmin__Email` y `BootstrapAdmin__Password` en lugar de user-secrets.

**Base de datos existente:** la migración `AddBusinessTables` crea las tablas comerciales. Fue verificada en una base vacía; **no** ejecutes `database update` contra una base que ya contenga esas tablas sin respaldarla y conciliar su historial de migraciones y esquema. Las migraciones no se aplican automáticamente al arrancar.

## Límites actuales

- La cuenta registrada públicamente no tiene acceso a los CRUD hasta que un administrador le asigne el rol `Admin` desde «Usuarios y roles». No se asigna ningún rol automáticamente.
- El formulario de cotización registra el usuario y la fecha en el servidor, pero el importe sigue siendo manual hasta implementar líneas de productos y cálculo. No presupongas IGV, stock o pedidos operativos.
- El envío de correos es simulado (`FakeEmailSender`); la recuperación por correo no está disponible.
- `dotnet build ProyectoFinal.sln` compila la aplicación y `dotnet test ProyectoFinal.sln` ejecuta pruebas de navegación, permisos, formularios y antifalsificación con una base EF en memoria. Las pruebas no reemplazan una verificación con SQL Server real.
