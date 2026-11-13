using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProyectoFinal.Data;

namespace ProyectoFinal.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261104160000_InventorySubmenuPages")]
public sealed class InventorySubmenuPages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE @Administracion int, @Catalogos int, @Almacen int, @Inventario int;
            SELECT @Administracion = idMenu FROM dbo.MenuSistema WHERE codigo = N'administracion';
            SELECT @Catalogos = idMenu FROM dbo.MenuSistema WHERE codigo = N'catalogos-sunat';
            SELECT @Almacen = idMenu FROM dbo.MenuSistema WHERE codigo = N'almacen';
            SELECT @Inventario = idMenu FROM dbo.MenuSistema WHERE codigo = N'inventario';
            IF @Administracion IS NULL OR @Catalogos IS NULL OR @Almacen IS NULL OR @Inventario IS NULL
                THROW 51060, 'No se encontró la estructura de navegación esperada.', 1;

            -- These labels were written by a SQL client with the wrong ANSI code page.
            UPDATE dbo.MenuSistema SET nombre = N'Administraci' + NCHAR(243) + N'n'
                WHERE idMenu = @Administracion;
            UPDATE dbo.MenuSistema SET nombre = N'Cat' + NCHAR(225) + N'logos SUNAT'
                WHERE idMenu = @Catalogos;
            UPDATE dbo.MenuSistema SET nombre = N'Almac' + NCHAR(233) + N'n'
                WHERE idMenu = @Almacen;

            UPDATE dbo.MenuSistema SET nombre = N'Transferencias', orden = 40, idMenuPadre = @Inventario,
                controllerNombre = N'Transferencias', destino = N'transferencias', activo = 1
                WHERE codigo = N'transferencias';

            IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'inventario-conteos')
                INSERT dbo.MenuSistema (codigo,nombre,controllerNombre,orden,activo,idMenuPadre,destino,icono)
                VALUES (N'inventario-conteos',N'Conteo f' + NCHAR(237) + N'sico en curso',N'Inventario',10,1,
                    @Inventario,N'inventario-conteos',N'grid');
            IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'inventario-cortes')
                INSERT dbo.MenuSistema (codigo,nombre,controllerNombre,orden,activo,idMenuPadre,destino,icono)
                VALUES (N'inventario-cortes',N'Cortes',N'Inventario',20,1,@Inventario,N'inventario-cortes',N'layers');
            IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'inventario-kardex')
                INSERT dbo.MenuSistema (codigo,nombre,controllerNombre,orden,activo,idMenuPadre,destino,icono)
                VALUES (N'inventario-kardex',N'K' + NCHAR(225) + N'rdex reciente',N'Inventario',30,1,
                    @Inventario,N'inventario-kardex',N'grid');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.PermisoRolMenu p INNER JOIN dbo.MenuSistema m ON m.idMenu = p.idMenu
                       WHERE m.codigo IN (N'inventario-conteos', N'inventario-cortes', N'inventario-kardex'))
                THROW 51061, 'Revoque primero los permisos de las nuevas páginas de inventario.', 1;
            DELETE dbo.MenuSistema WHERE codigo IN (N'inventario-conteos', N'inventario-cortes', N'inventario-kardex');
            UPDATE dbo.MenuSistema SET orden = 10 WHERE codigo = N'transferencias';
            """);
    }
}
