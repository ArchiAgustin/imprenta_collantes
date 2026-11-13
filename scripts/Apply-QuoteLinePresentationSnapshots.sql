-- Apply only after a fresh full backup and verification.
-- Expected latest migration: 20261105120000_EmailConfigurationParametersMenu.
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

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId=N'20261105120000_EmailConfigurationParametersMenu')
    THROW 51120, 'Unexpected migration history; reconcile before applying.', 1;
IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
           WHERE MigrationId=N'20261106100000_QuoteLinePresentationSnapshots')
    THROW 51121, 'Quote presentation snapshots migration is already applied.', 1;
IF COL_LENGTH(N'dbo.DetalleCotizacion',N'factorPresentacion') IS NOT NULL
   OR COL_LENGTH(N'dbo.DetalleVenta',N'factorPresentacion') IS NOT NULL
   OR COL_LENGTH(N'dbo.DetallePedido',N'factorPresentacion') IS NOT NULL
    THROW 51122, 'Presentation snapshot columns already exist; reconcile schema/history.', 1;

ALTER TABLE dbo.DetalleCotizacion ADD
    idPresentacionProducto int NULL,
    factorPresentacion decimal(12,3) NOT NULL CONSTRAINT DF_DetalleCotizacion_factorPresentacion DEFAULT(1),
    presentacionDescripcion nvarchar(100) NULL,
    unidadBaseDescripcion nvarchar(100) NULL;
ALTER TABLE dbo.DetalleVenta ADD
    idPresentacionProducto int NULL,
    factorPresentacion decimal(12,3) NOT NULL CONSTRAINT DF_DetalleVenta_factorPresentacion DEFAULT(1),
    presentacionDescripcion nvarchar(100) NULL,
    unidadBaseDescripcion nvarchar(100) NULL;
ALTER TABLE dbo.DetallePedido ADD
    idPresentacionProducto int NULL,
    factorPresentacion decimal(12,3) NOT NULL CONSTRAINT DF_DetallePedido_factorPresentacion DEFAULT(1),
    presentacionDescripcion nvarchar(100) NULL,
    unidadBaseDescripcion nvarchar(100) NULL;

CREATE INDEX IX_DetalleCotizacion_idPresentacionProducto ON dbo.DetalleCotizacion(idPresentacionProducto);
CREATE INDEX IX_DetalleVenta_idPresentacionProducto ON dbo.DetalleVenta(idPresentacionProducto);
CREATE INDEX IX_DetallePedido_idPresentacionProducto ON dbo.DetallePedido(idPresentacionProducto);
ALTER TABLE dbo.DetalleCotizacion WITH CHECK ADD CONSTRAINT FK_DetalleCotizacion_PresentacionProducto_idPresentacionProducto
    FOREIGN KEY(idPresentacionProducto) REFERENCES dbo.PresentacionProducto(idPresentacionProducto) ON DELETE NO ACTION;
ALTER TABLE dbo.DetalleVenta WITH CHECK ADD CONSTRAINT FK_DetalleVenta_PresentacionProducto_idPresentacionProducto
    FOREIGN KEY(idPresentacionProducto) REFERENCES dbo.PresentacionProducto(idPresentacionProducto) ON DELETE NO ACTION;
ALTER TABLE dbo.DetallePedido WITH CHECK ADD CONSTRAINT FK_DetallePedido_PresentacionProducto_idPresentacionProducto
    FOREIGN KEY(idPresentacionProducto) REFERENCES dbo.PresentacionProducto(idPresentacionProducto) ON DELETE NO ACTION;
GO

INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion)
VALUES(N'20261106100000_QuoteLinePresentationSnapshots',N'8.0.15');
COMMIT TRANSACTION;
GO
