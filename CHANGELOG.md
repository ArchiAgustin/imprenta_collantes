# Changelog

Todos los cambios relevantes del proyecto se registran en este archivo.
Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y [versionado semántico](https://semver.org/lang/es/).

## [Sin publicar]

## [1.0.1] - pendiente de publicación

### Añadido
- CR-00: guía de contribución (`CONTRIBUTING.md`), registro de cambios, fichas de solicitudes de cambio en `docs/cambios/` y pipeline de GitHub Actions que ejecuta las pruebas con .NET 8.
- CR-00: documentación de la arquitectura del módulo de identificación (`docs/arquitectura.md`).
- CR-01: 5 pruebas de caracterización de DNI y RUC (`ValidadorDocumentoCaracterizacionTests.cs`).

### Cambiado
- CR-01: la validación de formato y la normalización del documento se extraen de `IdentificacionService` a la nueva clase `ValidadorDocumentoIdentidad` (SRP). Sin cambios de comportamiento ni de mensajes.

## [1.0.0] - 2026-11-14

### Añadido
- Línea base del sistema Imprenta Collantes: gestión de clientes, proveedores, productos, cotizaciones, ventas y pedidos (ASP.NET Core 8 MVC, EF Core 8, SQL Server).
