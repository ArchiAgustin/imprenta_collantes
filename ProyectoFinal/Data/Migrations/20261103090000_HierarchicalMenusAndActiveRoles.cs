using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class HierarchicalMenusAndActiveRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "controllerNombre",
                table: "MenuSistema",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "destino",
                table: "MenuSistema",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "icono",
                table: "MenuSistema",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "idMenuPadre",
                table: "MenuSistema",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuSistema_destino",
                table: "MenuSistema",
                column: "destino",
                unique: true,
                filter: "[destino] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSistema_idMenuPadre",
                table: "MenuSistema",
                column: "idMenuPadre");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuSistema_MenuSistema_idMenuPadre",
                table: "MenuSistema",
                column: "idMenuPadre",
                principalTable: "MenuSistema",
                principalColumn: "idMenu",
                onDelete: ReferentialAction.Restrict);

            // Preserve the existing menu IDs and role grants. Add grouping nodes and
            // reviewed destinations around the current catalog rather than replacing it.
            migrationBuilder.Sql("""
                DECLARE @Comercial int, @Produccion int, @Administracion int, @Accesos int, @Configuracion int;

                IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'comercial')
                    INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                    VALUES (N'comercial', N'Comercial', NULL, 10, 1, NULL, NULL, N'layers');
                IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'produccion')
                    INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                    VALUES (N'produccion', N'Producción', NULL, 20, 1, NULL, NULL, N'grid');
                IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'administracion')
                    INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                    VALUES (N'administracion', N'Administración', NULL, 90, 1, NULL, NULL, N'settings');

                SELECT @Comercial = idMenu FROM dbo.MenuSistema WHERE codigo = N'comercial';
                SELECT @Produccion = idMenu FROM dbo.MenuSistema WHERE codigo = N'produccion';
                SELECT @Administracion = idMenu FROM dbo.MenuSistema WHERE codigo = N'administracion';

                IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'accesos')
                    INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                    VALUES (N'accesos', N'Accesos', NULL, 10, 1, @Administracion, NULL, N'shield');
                IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'configuracion')
                    INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                    VALUES (N'configuracion', N'Configuración', NULL, 20, 1, @Administracion, NULL, N'sliders');
                SELECT @Accesos = idMenu FROM dbo.MenuSistema WHERE codigo = N'accesos';
                SELECT @Configuracion = idMenu FROM dbo.MenuSistema WHERE codigo = N'configuracion';

                UPDATE dbo.MenuSistema
                SET idMenuPadre = CASE codigo WHEN N'taller' THEN @Produccion ELSE @Comercial END,
                    destino = CASE codigo WHEN N'clientes' THEN N'clientes' WHEN N'proveedores' THEN N'proveedores'
                        WHEN N'productos' THEN N'productos' WHEN N'cotizaciones' THEN N'cotizaciones'
                        WHEN N'ventas' THEN N'ventas' WHEN N'taller' THEN N'taller' END,
                    icono = CASE codigo WHEN N'clientes' THEN N'users' WHEN N'proveedores' THEN N'folder'
                        WHEN N'productos' THEN N'layers' WHEN N'cotizaciones' THEN N'folder'
                        WHEN N'ventas' THEN N'grid' WHEN N'taller' THEN N'bolt' END
                WHERE codigo IN (N'clientes', N'proveedores', N'productos', N'cotizaciones', N'ventas', N'taller');

                DECLARE @Cotizaciones int, @PreCotizacion int;
                SELECT @Cotizaciones = idMenu FROM dbo.MenuSistema WHERE codigo = N'cotizaciones';
                IF NOT EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'precotizacion')
                    INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                    VALUES (N'precotizacion', N'Pre-cotización con IA', N'PreCotizacion', 10, 1, @Cotizaciones, N'precotizacion', N'spark');
                ELSE
                    UPDATE dbo.MenuSistema SET nombre = N'Pre-cotización con IA', controllerNombre = N'PreCotizacion',
                        idMenuPadre = @Cotizaciones, destino = N'precotizacion', activo = 1
                    WHERE codigo = N'precotizacion';
                SELECT @PreCotizacion = idMenu FROM dbo.MenuSistema WHERE codigo = N'precotizacion';

                INSERT dbo.PermisoRolMenu (idMenu, idRol, ver, crear, editar, eliminar, procesar)
                SELECT @PreCotizacion, source.idRol, source.ver, source.crear, source.editar, source.eliminar, source.procesar
                FROM dbo.PermisoRolMenu AS source
                WHERE source.idMenu = @Cotizaciones
                    AND NOT EXISTS (SELECT 1 FROM dbo.PermisoRolMenu AS existing
                        WHERE existing.idMenu = @PreCotizacion AND existing.idRol = source.idRol);

                DECLARE @NewMenus TABLE (codigo nvarchar(64), nombre nvarchar(100), controllerNombre nvarchar(100),
                    destino nvarchar(64), parentId int, orden int, icono nvarchar(32));
                INSERT @NewMenus VALUES
                    (N'inventario', N'Inventario', N'Inventario', N'inventario', @Produccion, 20, N'layers'),
                    (N'usuarios', N'Usuarios', N'Admin', N'usuarios', @Accesos, 10, N'users'),
                    (N'roles', N'Roles', N'Roles', N'roles', @Accesos, 20, N'shield'),
                    (N'permisos', N'Accesos por rol', N'Permisos', N'permisos', @Accesos, 30, N'sliders'),
                    (N'menus', N'Menús', N'Menus', N'menus', @Configuracion, 10, N'layers'),
                    (N'series-internas', N'Series internas', N'SeriesInternas', N'series', @Configuracion, 20, N'grid'),
                    (N'auditoria', N'Auditoría', N'Auditoria', N'auditoria', @Administracion, 30, N'folder'),
                    (N'analitica', N'Analítica', N'Analytics', N'analitica', @Administracion, 40, N'grid');

                UPDATE menu SET menu.nombre = incoming.nombre, menu.controllerNombre = incoming.controllerNombre,
                    menu.destino = incoming.destino, menu.idMenuPadre = incoming.parentId,
                    menu.orden = incoming.orden, menu.icono = incoming.icono
                FROM dbo.MenuSistema AS menu INNER JOIN @NewMenus AS incoming ON incoming.codigo = menu.codigo;

                INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                SELECT incoming.codigo, incoming.nombre, incoming.controllerNombre, incoming.orden, 1,
                    incoming.parentId, incoming.destino, incoming.icono
                FROM @NewMenus AS incoming
                WHERE NOT EXISTS (SELECT 1 FROM dbo.MenuSistema AS menu WHERE menu.codigo = incoming.codigo);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE dbo.MenuSistema SET controllerNombre = N'' WHERE controllerNombre IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuSistema_MenuSistema_idMenuPadre",
                table: "MenuSistema");

            migrationBuilder.DropIndex(
                name: "IX_MenuSistema_destino",
                table: "MenuSistema");

            migrationBuilder.DropIndex(
                name: "IX_MenuSistema_idMenuPadre",
                table: "MenuSistema");

            migrationBuilder.DropColumn(
                name: "destino",
                table: "MenuSistema");

            migrationBuilder.DropColumn(
                name: "icono",
                table: "MenuSistema");

            migrationBuilder.DropColumn(
                name: "idMenuPadre",
                table: "MenuSistema");

            migrationBuilder.AlterColumn<string>(
                name: "controllerNombre",
                table: "MenuSistema",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
