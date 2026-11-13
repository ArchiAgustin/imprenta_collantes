-- Apply only after a fresh full backup and verification.
-- Expected latest migration: 20261104200000_InventoryHeadersDetailsAndSeries.
-- Applies the two pending additive migrations in order.
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
               WHERE MigrationId=N'20261104200000_InventoryHeadersDetailsAndSeries')
    THROW 51100, 'Unexpected migration history; reconcile before applying.', 1;
IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
           WHERE MigrationId IN (N'20261105100000_UnifiedSupplierIdentity', N'20261105120000_EmailConfigurationParametersMenu'))
    THROW 51101, 'At least one target migration is already applied; reconcile before continuing.', 1;
IF COL_LENGTH(N'dbo.Proveedor', N'idDocumentoIdentidadTipo') IS NOT NULL
   OR COL_LENGTH(N'dbo.Proveedor', N'numeroDocumento') IS NOT NULL
    THROW 51102, 'Supplier identity columns already exist; reconcile schema/history.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.documentoidentidad_tipo WHERE codigo=N'6')
    THROW 51103, 'SUNAT identity document type RUC (code 6) is missing.', 1;

ALTER TABLE dbo.Proveedor ADD idDocumentoIdentidadTipo int NULL;
ALTER TABLE dbo.Proveedor ADD numeroDocumento nvarchar(20) NULL;
UPDATE provider
SET provider.idDocumentoIdentidadTipo = documentType.iddocumentoidentidadtipo,
    provider.numeroDocumento = provider.ruc
FROM dbo.Proveedor AS provider
INNER JOIN dbo.documentoidentidad_tipo AS documentType ON documentType.codigo=N'6'
WHERE provider.ruc IS NOT NULL;

CREATE UNIQUE INDEX IX_Proveedor_idDocumentoIdentidadTipo_numeroDocumento
    ON dbo.Proveedor(idDocumentoIdentidadTipo, numeroDocumento)
    WHERE idDocumentoIdentidadTipo IS NOT NULL AND numeroDocumento IS NOT NULL;
ALTER TABLE dbo.Proveedor WITH CHECK ADD CONSTRAINT FK_Proveedor_documentoidentidad_tipo_idDocumentoIdentidadTipo
    FOREIGN KEY(idDocumentoIdentidadTipo) REFERENCES dbo.documentoidentidad_tipo(iddocumentoidentidadtipo) ON DELETE NO ACTION;
GO

CREATE TABLE dbo.ConfiguracionCorreo
(
    id int NOT NULL,
    servidor nvarchar(250) NOT NULL,
    puerto int NOT NULL,
    correoRemitente nvarchar(254) NOT NULL,
    nombreRemitente nvarchar(100) NOT NULL,
    usuario nvarchar(250) NULL,
    contrasenaProtegida nvarchar(2000) NULL,
    usarTls bit NOT NULL,
    actualizadoUtc datetimeoffset NOT NULL,
    CONSTRAINT PK_ConfiguracionCorreo PRIMARY KEY(id),
    CONSTRAINT CK_ConfiguracionCorreo_Id CHECK(id=1),
    CONSTRAINT CK_ConfiguracionCorreo_Puerto CHECK(puerto BETWEEN 1 AND 65535)
);

DECLARE @ConfiguracionId int = (SELECT TOP (1) idMenu FROM dbo.MenuSistema WHERE codigo=N'configuracion');
IF @ConfiguracionId IS NULL
    THROW 51104, 'Configuration menu was not found; reconcile the menu tree.', 1;
IF EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE destino=N'parametros' AND codigo<>N'parametros')
    THROW 51105, 'The parametros destination is already assigned to another menu.', 1;
IF EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo=N'parametros')
    UPDATE dbo.MenuSistema SET nombre=N'Parámetros', idMenuPadre=@ConfiguracionId, icono=N'sliders',
        destino=N'parametros', controllerNombre=N'Parametros', orden=30, activo=1
    WHERE codigo=N'parametros';
ELSE
    INSERT dbo.MenuSistema(codigo,nombre,idMenuPadre,icono,destino,controllerNombre,orden,activo)
    VALUES(N'parametros',N'Parámetros',@ConfiguracionId,N'sliders',N'parametros',N'Parametros',30,1);
GO

INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion)
VALUES(N'20261105100000_UnifiedSupplierIdentity',N'8.0.15');
INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion)
VALUES(N'20261105120000_EmailConfigurationParametersMenu',N'8.0.15');
COMMIT TRANSACTION;
GO
