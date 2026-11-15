# Arquitectura del módulo de identificación

El sistema Imprenta Collantes usa ASP.NET Core 8 MVC, EF Core 8 y SQL Server. El registro de clientes/proveedores pasa por controladores y `IdentificacionService`, que consulta los tipos de documento y el ubigeo en `ApplicationDbContext` (`ClientesController.cs:57-78`; `IdentificacionService.cs:17-43,46-117`).

```mermaid
flowchart LR
    Usuario --> ClientesController
    Usuario --> ProveedorsController
    ClientesController --> IdentificacionService
    ProveedorsController --> IdentificacionService
    ClientesController --> ClientesCatalogoService
    IdentificacionService --> ApplicationDbContext
    ClientesCatalogoService --> ApplicationDbContext
    ApplicationDbContext --> SQLServer
```

```mermaid
sequenceDiagram
    actor Usuario
    participant Vista as Clientes/Create
    participant Controller as ClientesController
    participant Identificacion as IdentificacionService
    participant Catalogo as ClientesCatalogoService
    participant DB as ApplicationDbContext
    Usuario->>Vista: Completa DNI/RUC, nombre y ubigeo
    Vista->>Controller: POST Create
    Controller->>Identificacion: ValidarClienteAsync
    Identificacion->>DB: Consulta tipo, duplicados y distrito
    alt Modelo válido
        Controller->>Catalogo: CrearAsync
        Catalogo->>DB: SaveChangesAsync
    else Modelo inválido
        Controller->>Identificacion: CargarCatalogosAsync
        Controller-->>Vista: Errores y listas
    end
```
