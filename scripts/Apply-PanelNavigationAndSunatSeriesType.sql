-- Apply after a fresh full backup and verification.
-- Expected latest migration: 20261104120000_MultiWarehouseOperations.
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
           WHERE MigrationId = N'20261104140000_PanelNavigationAndSunatSeriesType')
    THROW 51050, 'PanelNavigationAndSunatSeriesType is already applied.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId = N'20261104120000_MultiWarehouseOperations')
    THROW 51051, 'Unexpected migration history; reconcile before applying.', 1;

ALTER TABLE dbo.SerieDocumentoInterno ADD idTipoComprobanteSunat int NULL;
CREATE INDEX IX_SerieDocumentoInterno_idTipoComprobanteSunat
    ON dbo.SerieDocumentoInterno(idTipoComprobanteSunat);
ALTER TABLE dbo.SerieDocumentoInterno ADD CONSTRAINT FK_SerieDocumentoInterno_mae_tipodocumentopago_idTipoComprobanteSunat
    FOREIGN KEY(idTipoComprobanteSunat) REFERENCES dbo.mae_tipodocumentopago(idtipodocumentopago) ON DELETE NO ACTION;
GO

DECLARE @Administracion int, @Catalogos int, @Almacen int, @Comercial int;
SELECT @Administracion = idMenu FROM dbo.MenuSistema WHERE codigo = N'administracion';
SELECT @Catalogos = idMenu FROM dbo.MenuSistema WHERE codigo = N'catalogos-sunat';
SELECT @Comercial = idMenu FROM dbo.MenuSistema WHERE codigo = N'comercial';
IF @Administracion IS NULL OR @Catalogos IS NULL OR @Comercial IS NULL
    THROW 51052, 'Expected menu structure was not found.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'almacen')
    INSERT dbo.MenuSistema (codigo,nombre,controllerNombre,orden,activo,idMenuPadre,destino,icono)
    VALUES (N'almacen',N'Almac' + NCHAR(233) + N'n',NULL,30,1,NULL,NULL,N'layers');
SELECT @Almacen = idMenu FROM dbo.MenuSistema WHERE codigo = N'almacen';

UPDATE dbo.MenuSistema SET nombre=N'Administraci' + NCHAR(243) + N'n',orden=10,idMenuPadre=NULL,activo=1 WHERE idMenu=@Administracion;
UPDATE dbo.MenuSistema SET nombre=N'Cat' + NCHAR(225) + N'logos SUNAT',orden=20,idMenuPadre=NULL,activo=1 WHERE idMenu=@Catalogos;
UPDATE dbo.MenuSistema SET nombre=N'Almac' + NCHAR(233) + N'n',orden=30,idMenuPadre=NULL,
    controllerNombre=NULL,destino=NULL,activo=1 WHERE idMenu=@Almacen;
UPDATE dbo.MenuSistema SET nombre=N'Comercial',orden=40,idMenuPadre=NULL,activo=1 WHERE idMenu=@Comercial;
UPDATE dbo.MenuSistema SET activo=0 WHERE codigo=N'produccion';

UPDATE dbo.MenuSistema SET idMenuPadre=@Comercial,orden=CASE codigo
    WHEN N'clientes' THEN 10 WHEN N'proveedores' THEN 20 WHEN N'cotizaciones' THEN 30
    WHEN N'ventas' THEN 40 WHEN N'taller' THEN 50 ELSE orden END
    WHERE codigo IN (N'clientes',N'proveedores',N'cotizaciones',N'ventas',N'taller');
UPDATE dbo.MenuSistema SET idMenuPadre=@Almacen,orden=CASE codigo
    WHEN N'almacenes' THEN 10 WHEN N'inventario' THEN 20 WHEN N'productos' THEN 30 ELSE orden END
    WHERE codigo IN (N'almacenes',N'inventario',N'productos');

IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo=N'almacenes')
    INSERT dbo.MenuSistema (codigo,nombre,controllerNombre,orden,activo,idMenuPadre,destino,icono)
    VALUES (N'almacenes',N'Almacenes',N'Almacenes',10,1,@Almacen,N'almacenes',N'layers');
IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo=N'inventario')
    INSERT dbo.MenuSistema (codigo,nombre,controllerNombre,orden,activo,idMenuPadre,destino,icono)
    VALUES (N'inventario',N'Inventario',N'Inventario',20,1,@Almacen,N'inventario',N'grid');
IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo=N'transferencias')
    INSERT dbo.MenuSistema (codigo,nombre,controllerNombre,orden,activo,idMenuPadre,destino,icono)
    VALUES (N'transferencias',N'Transferencias',N'Transferencias',10,1,
        (SELECT idMenu FROM dbo.MenuSistema WHERE codigo=N'inventario'),N'transferencias',N'grid');
UPDATE dbo.MenuSistema SET nombre=N'Series' WHERE codigo=N'series-internas' AND destino=N'series';
GO

INSERT INTO dbo.__EFMigrationsHistory(MigrationId,ProductVersion)
VALUES(N'20261104140000_PanelNavigationAndSunatSeriesType',N'8.0.15');
COMMIT TRANSACTION;
GO
