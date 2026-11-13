using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProyectoFinal.Data;

namespace ProyectoFinal.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261104140000_PanelNavigationAndSunatSeriesType")]
public sealed class PanelNavigationAndSunatSeriesType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "idTipoComprobanteSunat",
            table: "SerieDocumentoInterno",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SerieDocumentoInterno_idTipoComprobanteSunat",
            table: "SerieDocumentoInterno",
            column: "idTipoComprobanteSunat");

        migrationBuilder.AddForeignKey(
            name: "FK_SerieDocumentoInterno_mae_tipodocumentopago_idTipoComprobanteSunat",
            table: "SerieDocumentoInterno",
            column: "idTipoComprobanteSunat",
            principalTable: "mae_tipodocumentopago",
            principalColumn: "idtipodocumentopago",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql("""
            DECLARE @Administracion int, @Catalogos int, @Almacen int, @Comercial int;

            SELECT @Administracion = idMenu FROM dbo.MenuSistema WHERE codigo = N'administracion';
            SELECT @Catalogos = idMenu FROM dbo.MenuSistema WHERE codigo = N'catalogos-sunat';
            SELECT @Comercial = idMenu FROM dbo.MenuSistema WHERE codigo = N'comercial';

            IF @Administracion IS NULL OR @Catalogos IS NULL OR @Comercial IS NULL
                THROW 51040, 'No se encontró la estructura de menú esperada; reconcile antes de reordenar.', 1;

            IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'almacen')
                INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                VALUES (N'almacen', N'Almacén', NULL, 30, 1, NULL, NULL, N'layers');
            SELECT @Almacen = idMenu FROM dbo.MenuSistema WHERE codigo = N'almacen';

            UPDATE dbo.MenuSistema SET nombre = N'Administración', orden = 10, idMenuPadre = NULL, activo = 1
                WHERE idMenu = @Administracion;
            UPDATE dbo.MenuSistema SET nombre = N'Catálogos SUNAT', orden = 20, idMenuPadre = NULL, activo = 1
                WHERE idMenu = @Catalogos;
            UPDATE dbo.MenuSistema SET nombre = N'Almacén', orden = 30, idMenuPadre = NULL, controllerNombre = NULL,
                destino = NULL, activo = 1 WHERE idMenu = @Almacen;
            UPDATE dbo.MenuSistema SET nombre = N'Comercial', orden = 40, idMenuPadre = NULL, activo = 1
                WHERE idMenu = @Comercial;

            UPDATE dbo.MenuSistema SET activo = 0 WHERE codigo = N'produccion';

            UPDATE dbo.MenuSistema SET idMenuPadre = @Comercial, orden = CASE codigo
                WHEN N'clientes' THEN 10 WHEN N'proveedores' THEN 20 WHEN N'cotizaciones' THEN 30
                WHEN N'ventas' THEN 40 WHEN N'taller' THEN 50 ELSE orden END
                WHERE codigo IN (N'clientes', N'proveedores', N'cotizaciones', N'ventas', N'taller');
            UPDATE dbo.MenuSistema SET idMenuPadre = @Almacen, orden = CASE codigo
                WHEN N'almacenes' THEN 10 WHEN N'inventario' THEN 20 WHEN N'productos' THEN 30 ELSE orden END
                WHERE codigo IN (N'almacenes', N'inventario', N'productos');

            IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'almacenes')
                INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                VALUES (N'almacenes', N'Almacenes', N'Almacenes', 10, 1, @Almacen, N'almacenes', N'layers');
            IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'inventario')
                INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                VALUES (N'inventario', N'Inventario', N'Inventario', 20, 1, @Almacen, N'inventario', N'grid');
            IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'transferencias')
                INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                VALUES (N'transferencias', N'Transferencias', N'Transferencias', 10, 1,
                    (SELECT idMenu FROM dbo.MenuSistema WHERE codigo = N'inventario'), N'transferencias', N'grid');

            UPDATE dbo.MenuSistema SET nombre = N'Series'
                WHERE codigo = N'series-internas' AND destino = N'series';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.PermisoRolMenu p INNER JOIN dbo.MenuSistema m ON m.idMenu = p.idMenu
                       WHERE m.codigo IN (N'almacen', N'almacenes', N'transferencias'))
                THROW 51041, 'Revoque primero los permisos de los nuevos menús.', 1;
            DELETE dbo.MenuSistema WHERE codigo IN (N'transferencias', N'almacenes', N'almacen');
            UPDATE dbo.MenuSistema SET nombre = N'Series internas' WHERE codigo = N'series-internas';
            UPDATE dbo.MenuSistema SET activo = 1 WHERE codigo = N'produccion';
            """);
        migrationBuilder.DropForeignKey(
            name: "FK_SerieDocumentoInterno_mae_tipodocumentopago_idTipoComprobanteSunat",
            table: "SerieDocumentoInterno");
        migrationBuilder.DropIndex(name: "IX_SerieDocumentoInterno_idTipoComprobanteSunat", table: "SerieDocumentoInterno");
        migrationBuilder.DropColumn(name: "idTipoComprobanteSunat", table: "SerieDocumentoInterno");
    }
}
