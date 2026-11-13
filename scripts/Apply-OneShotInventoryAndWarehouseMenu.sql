-- Apply after a fresh full backup and verification.
-- Expected latest migration: 20261104160000_InventorySubmenuPages.
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
           WHERE MigrationId=N'20261104180000_OneShotInventoryAndWarehouseMenu')
    THROW 51090, 'OneShotInventoryAndWarehouseMenu is already applied.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId=N'20261104160000_InventorySubmenuPages')
    THROW 51091, 'Unexpected migration history; reconcile before applying.', 1;

DECLARE @Almacen int, @Inventario int;
SELECT @Almacen=idMenu FROM dbo.MenuSistema WHERE codigo=N'almacen';
SELECT @Inventario=idMenu FROM dbo.MenuSistema WHERE codigo=N'inventario';
IF @Almacen IS NULL OR @Inventario IS NULL
    THROW 51092, 'Expected menu structure was not found.', 1;

UPDATE dbo.MenuSistema SET idMenuPadre=@Almacen,orden=10 WHERE codigo=N'almacenes';
UPDATE dbo.MenuSistema SET idMenuPadre=@Almacen,orden=20 WHERE codigo=N'productos';
UPDATE dbo.MenuSistema SET idMenuPadre=@Almacen,orden=30 WHERE idMenu=@Inventario;
UPDATE dbo.MenuSistema SET idMenuPadre=@Almacen,orden=40 WHERE codigo=N'inventario-kardex';
UPDATE dbo.MenuSistema SET idMenuPadre=@Almacen,orden=50 WHERE codigo=N'transferencias';

UPDATE menu SET activo=0,idMenuPadre=NULL,controllerNombre=NULL,destino=NULL
    FROM dbo.MenuSistema menu
    WHERE menu.codigo IN (N'inventario-conteos',N'inventario-cortes')
      AND EXISTS (SELECT 1 FROM dbo.PermisoRolMenu p WHERE p.idMenu=menu.idMenu);
DELETE menu FROM dbo.MenuSistema menu
    WHERE menu.codigo IN (N'inventario-conteos',N'inventario-cortes')
      AND NOT EXISTS (SELECT 1 FROM dbo.PermisoRolMenu p WHERE p.idMenu=menu.idMenu);

UPDATE dbo.CorteInventario
SET estado=N'Anulado',observaciones=CASE WHEN NULLIF(LTRIM(RTRIM(observaciones)),N'') IS NULL
    THEN N'Borrador del flujo anterior anulado al pasar a cortes de una sola operaci'+NCHAR(243)+N'n; no modific'+NCHAR(243)+N' existencias.'
    ELSE LEFT(observaciones,360)+N' | Flujo anterior anulado; no modific'+NCHAR(243)+N' existencias.' END
WHERE estado=N'Borrador';
UPDATE dbo.Almacen SET conteoEnCurso=0 WHERE conteoEnCurso=1;
GO

INSERT INTO dbo.__EFMigrationsHistory(MigrationId,ProductVersion)
VALUES(N'20261104180000_OneShotInventoryAndWarehouseMenu',N'8.0.15');
COMMIT TRANSACTION;
GO
