using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProyectoFinal.Data;

namespace ProyectoFinal.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261104180000_OneShotInventoryAndWarehouseMenu")]
public sealed class OneShotInventoryAndWarehouseMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE @Almacen int, @Inventario int;
            SELECT @Almacen = idMenu FROM dbo.MenuSistema WHERE codigo = N'almacen';
            SELECT @Inventario = idMenu FROM dbo.MenuSistema WHERE codigo = N'inventario';
            IF @Almacen IS NULL OR @Inventario IS NULL
                THROW 51080, 'No se encontró la estructura de menú de Almacén.', 1;

            -- Inventory, Kardex and transfers are sibling destinations under Almacén.
            UPDATE dbo.MenuSistema SET idMenuPadre = @Almacen, orden = 10
                WHERE codigo = N'almacenes';
            UPDATE dbo.MenuSistema SET idMenuPadre = @Almacen, orden = 20
                WHERE codigo = N'productos';
            UPDATE dbo.MenuSistema SET idMenuPadre = @Almacen, orden = 30
                WHERE idMenu = @Inventario;
            UPDATE dbo.MenuSistema SET idMenuPadre = @Almacen, orden = 40
                WHERE codigo = N'inventario-kardex';
            UPDATE dbo.MenuSistema SET idMenuPadre = @Almacen, orden = 50
                WHERE codigo = N'transferencias';

            -- Remove obsolete count-in-progress/cuts pages without deleting role grants.
            UPDATE menu SET activo = 0, idMenuPadre = NULL, controllerNombre = NULL, destino = NULL
                FROM dbo.MenuSistema menu
                WHERE menu.codigo IN (N'inventario-conteos', N'inventario-cortes')
                  AND EXISTS (SELECT 1 FROM dbo.PermisoRolMenu p WHERE p.idMenu = menu.idMenu);
            DELETE menu FROM dbo.MenuSistema menu
                WHERE menu.codigo IN (N'inventario-conteos', N'inventario-cortes')
                  AND NOT EXISTS (SELECT 1 FROM dbo.PermisoRolMenu p WHERE p.idMenu = menu.idMenu);

            -- Preserve old draft rows and entered figures, but retire the old pause workflow.
            UPDATE dbo.CorteInventario
            SET estado = N'Anulado',
                observaciones = CASE WHEN NULLIF(LTRIM(RTRIM(observaciones)), N'') IS NULL
                    THEN N'Borrador del flujo anterior anulado al pasar a cortes de una sola operación; no modificó existencias.'
                    ELSE LEFT(observaciones, 360) + N' | Flujo anterior anulado; no modificó existencias.' END
            WHERE estado = N'Borrador';
            UPDATE dbo.Almacen SET conteoEnCurso = 0 WHERE conteoEnCurso = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drafts were retired while preserving their entered data. Do not restore the
        // old pause state or reactivate the removed in-progress-count menus.
    }
}
