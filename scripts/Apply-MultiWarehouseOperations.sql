-- Apply only after a fresh full backup has been created and verified.
-- Expected database state: latest migration 20261103110000_SunatReferenceCatalogs.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
GO

IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
           WHERE MigrationId = N'20261104120000_MultiWarehouseOperations')
    THROW 51001, 'MultiWarehouseOperations is already applied.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId = N'20261103110000_SunatReferenceCatalogs')
    THROW 51002, 'Unexpected migration history; reconcile before applying.', 1;
ALTER TABLE dbo.Almacen ADD predeterminado bit NOT NULL
    CONSTRAINT DF_Almacen_predeterminado DEFAULT (0);
GO

EXEC(N'UPDATE dbo.Almacen SET predeterminado = 1
       WHERE idAlmacen = (SELECT TOP (1) idAlmacen FROM dbo.Almacen
                          WHERE nombre = N''Almac'' + NCHAR(233) + N''n principal'' ORDER BY idAlmacen);
       IF (SELECT COUNT(*) FROM dbo.Almacen WHERE predeterminado = 1) <> 1
           THROW 51003, ''Expected exactly one default warehouse.'', 1;');
CREATE UNIQUE INDEX IX_Almacen_predeterminado
    ON dbo.Almacen(predeterminado) WHERE predeterminado = 1;

ALTER TABLE dbo.Venta ADD idAlmacen int NULL, almacenNombreEmision nvarchar(150) NULL;
CREATE INDEX IX_Venta_idAlmacen ON dbo.Venta(idAlmacen);
ALTER TABLE dbo.Venta ADD CONSTRAINT FK_Venta_Almacen_idAlmacen
    FOREIGN KEY(idAlmacen) REFERENCES dbo.Almacen(idAlmacen) ON DELETE NO ACTION;
GO

CREATE TABLE dbo.TransferenciaAlmacen
(
    idTransferenciaAlmacen int IDENTITY(1,1) NOT NULL,
    idAlmacenOrigen int NOT NULL,
    idAlmacenDestino int NOT NULL,
    idProducto int NOT NULL,
    cantidadBase decimal(18,3) NOT NULL,
    fechaUtc datetimeoffset NOT NULL,
    motivo nvarchar(500) NOT NULL,
    idUsuario nvarchar(450) NULL,
    CONSTRAINT PK_TransferenciaAlmacen PRIMARY KEY(idTransferenciaAlmacen),
    CONSTRAINT CK_TransferenciaAlmacen_Almacenes CHECK (idAlmacenOrigen <> idAlmacenDestino),
    CONSTRAINT FK_TransferenciaAlmacen_Almacen_idAlmacenOrigen
        FOREIGN KEY(idAlmacenOrigen) REFERENCES dbo.Almacen(idAlmacen) ON DELETE NO ACTION,
    CONSTRAINT FK_TransferenciaAlmacen_Almacen_idAlmacenDestino
        FOREIGN KEY(idAlmacenDestino) REFERENCES dbo.Almacen(idAlmacen) ON DELETE NO ACTION,
    CONSTRAINT FK_TransferenciaAlmacen_Producto_idProducto
        FOREIGN KEY(idProducto) REFERENCES dbo.Producto(idProducto) ON DELETE NO ACTION
);
CREATE INDEX IX_TransferenciaAlmacen_idAlmacenOrigen_fechaUtc
    ON dbo.TransferenciaAlmacen(idAlmacenOrigen, fechaUtc);
CREATE INDEX IX_TransferenciaAlmacen_idAlmacenDestino_fechaUtc
    ON dbo.TransferenciaAlmacen(idAlmacenDestino, fechaUtc);
CREATE INDEX IX_TransferenciaAlmacen_idProducto ON dbo.TransferenciaAlmacen(idProducto);
GO

INSERT INTO dbo.__EFMigrationsHistory(MigrationId, ProductVersion)
VALUES (N'20261104120000_MultiWarehouseOperations', N'8.0.15');
COMMIT TRANSACTION;
GO
