-- Apply after a fresh full backup and verification.
-- Expected latest migration: 20261104180000_OneShotInventoryAndWarehouseMenu.
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
           WHERE MigrationId=N'20261104200000_InventoryHeadersDetailsAndSeries')
    THROW 51100, 'InventoryHeadersDetailsAndSeries is already applied.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId=N'20261104180000_OneShotInventoryAndWarehouseMenu')
    THROW 51101, 'Unexpected migration history; reconcile before applying.', 1;

CREATE TABLE dbo.SerieInventario
(
    idSerieInventario int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SerieInventario PRIMARY KEY,
    serie nvarchar(8) NOT NULL,
    siguienteCorrelativo int NOT NULL,
    digitos int NOT NULL,
    activa bit NOT NULL,
    CONSTRAINT CK_SerieInventario_Correlativo CHECK (siguienteCorrelativo BETWEEN 1 AND 99999999),
    CONSTRAINT CK_SerieInventario_Digitos CHECK (digitos=8)
);
CREATE UNIQUE INDEX IX_SerieInventario_Serie ON dbo.SerieInventario(serie);
CREATE UNIQUE INDEX IX_SerieInventario_Activa ON dbo.SerieInventario(activa) WHERE activa=1;
GO

CREATE TABLE dbo.Inventario
(
    idInventario int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Inventario PRIMARY KEY,
    serie nvarchar(8) NOT NULL,
    correlativo int NOT NULL,
    fecha date NOT NULL,
    creadoUtc datetimeoffset NOT NULL,
    idAlmacen int NOT NULL,
    idUsuario nvarchar(450) NULL,
    idCorteInventarioOrigen int NULL,
    CONSTRAINT CK_Inventario_Correlativo CHECK (correlativo BETWEEN 1 AND 99999999),
    CONSTRAINT FK_Inventario_Almacen_IdAlmacen FOREIGN KEY(idAlmacen) REFERENCES dbo.Almacen(idAlmacen) ON DELETE NO ACTION,
    CONSTRAINT FK_Inventario_CorteInventario_IdCorteInventarioOrigen FOREIGN KEY(idCorteInventarioOrigen)
        REFERENCES dbo.CorteInventario(idCorteInventario) ON DELETE NO ACTION
);
CREATE UNIQUE INDEX IX_Inventario_Serie_Correlativo ON dbo.Inventario(serie,correlativo);
CREATE INDEX IX_Inventario_IdAlmacen_Fecha ON dbo.Inventario(idAlmacen,fecha);
CREATE UNIQUE INDEX IX_Inventario_IdCorteInventarioOrigen ON dbo.Inventario(idCorteInventarioOrigen)
    WHERE idCorteInventarioOrigen IS NOT NULL;
GO

CREATE TABLE dbo.InventarioDetalle
(
    idInventarioDetalle int IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventarioDetalle PRIMARY KEY,
    idInventario int NOT NULL,
    idProducto int NOT NULL,
    cantidadSistemaBase decimal(18,3) NULL,
    cantidadContadaBase decimal(18,3) NOT NULL,
    CONSTRAINT CK_InventarioDetalle_Cantidad CHECK (cantidadContadaBase>=0),
    CONSTRAINT CK_InventarioDetalle_Sistema CHECK (cantidadSistemaBase IS NULL OR cantidadSistemaBase>=0),
    CONSTRAINT FK_InventarioDetalle_Inventario_IdInventario FOREIGN KEY(idInventario) REFERENCES dbo.Inventario(idInventario) ON DELETE NO ACTION,
    CONSTRAINT FK_InventarioDetalle_Producto_IdProducto FOREIGN KEY(idProducto) REFERENCES dbo.Producto(idProducto) ON DELETE NO ACTION
);
CREATE UNIQUE INDEX IX_InventarioDetalle_IdInventario_IdProducto ON dbo.InventarioDetalle(idInventario,idProducto);
CREATE INDEX IX_InventarioDetalle_IdProducto ON dbo.InventarioDetalle(idProducto);
GO

DECLARE @LegacyMap TABLE (idCorteInventario int NOT NULL PRIMARY KEY, idInventario int NOT NULL);
;WITH CortesAprobados AS
(
    SELECT idCorteInventario,idAlmacen,fechaUtc,idUsuario,
        CONVERT(date,DATEADD(hour,-5,fechaUtc)) AS fecha,
        ROW_NUMBER() OVER (ORDER BY fechaUtc,idCorteInventario) AS correlativo
    FROM dbo.CorteInventario
    WHERE estado=N'Aprobado'
)
INSERT dbo.Inventario(serie,correlativo,fecha,creadoUtc,idAlmacen,idUsuario,idCorteInventarioOrigen)
OUTPUT inserted.idCorteInventarioOrigen,inserted.idInventario INTO @LegacyMap(idCorteInventario,idInventario)
SELECT N'INV1',correlativo,fecha,fechaUtc,idAlmacen,idUsuario,idCorteInventario FROM CortesAprobados;

INSERT dbo.InventarioDetalle(idInventario,idProducto,cantidadSistemaBase,cantidadContadaBase)
SELECT map.idInventario,detail.idProducto,detail.cantidadSistemaBase,detail.cantidadContadaBase
FROM @LegacyMap AS map
INNER JOIN dbo.CorteInventarioDetalle AS detail ON detail.idCorteInventario=map.idCorteInventario
WHERE detail.conteoRegistrado=1;

DECLARE @Siguiente int=ISNULL((SELECT MAX(correlativo)+1 FROM dbo.Inventario WHERE serie=N'INV1'),1);
IF @Siguiente>99999999 THROW 51102, 'The legacy inventory count exceeds the 8-digit INV1 range.', 1;
INSERT dbo.SerieInventario(serie,siguienteCorrelativo,digitos,activa) VALUES(N'INV1',@Siguiente,8,1);
GO

INSERT INTO dbo.__EFMigrationsHistory(MigrationId,ProductVersion)
VALUES(N'20261104200000_InventoryHeadersDetailsAndSeries',N'8.0.15');
COMMIT TRANSACTION;
GO
