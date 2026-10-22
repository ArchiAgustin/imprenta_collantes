# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

The currently supported operational user is an administrator. Publicly registered accounts have no access to the management modules until granted the Admin role. The SRS describes future sales and workshop roles; these are not part of the current access model.

## Product Purpose

Imprenta Collantes' existing application manages customers, suppliers, products and basic quotation records. Orders, analytics and AI are future requirements, not current features.

## Operating Context

Back-office CRUD workflows in Spanish; SQL Server is required for data and authentication. The SRS is in `Documentacion/REQUERIMIENTOS_SISTEMA.md`.

## Capabilities and Constraints

ASP.NET Core 8 MVC and Identity Razor Pages, Bootstrap and EF Core 8. Keep the current business data and routes while repairing workflows; do not imply that future SRS capabilities already work.

## Brand Commitments

The user requested a new functional identity for the admin panel, including an original logo and authored brand language. No existing brand assets or visual identity need preserving. Do not present invented performance figures or client testimonials as evidence.
