using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class VentasTallerYPermisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing installations were baselined with SQL Server's original FK names;
            // fresh installations use EF's conventional name. Resolve the actual constraint
            // by its relationship so the migration works on either schema.
            migrationBuilder.Sql("""
                DECLARE @fkName sysname;
                SELECT @fkName = fk.name
                FROM sys.foreign_keys AS fk
                INNER JOIN sys.foreign_key_columns AS col ON col.constraint_object_id = fk.object_id
                WHERE fk.parent_object_id = OBJECT_ID(N'dbo.DetallePedido')
                  AND fk.referenced_object_id = OBJECT_ID(N'dbo.Producto')
                  AND COL_NAME(col.parent_object_id, col.parent_column_id) = N'idProducto';

                IF @fkName IS NULL
                    THROW 50001, 'No se encontró la relación DetallePedido.idProducto -> Producto.', 1;

                DECLARE @dropSql nvarchar(max) = N'ALTER TABLE [dbo].[DetallePedido] DROP CONSTRAINT ' + QUOTENAME(@fkName);
                EXEC sys.sp_executesql @dropSql;
                """);

            migrationBuilder.AddColumn<bool>(
                name: "esServicio",
                table: "Producto",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "idVenta",
                table: "Pedido",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "esServicio",
                table: "DetallePedido",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "productoNombre",
                table: "DetallePedido",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estado",
                table: "Cotizacion",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Emitida");

            migrationBuilder.CreateTable(
                name: "AuditoriaOperacion",
                columns: table => new
                {
                    idAuditoria = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tipoEntidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    idEntidad = table.Column<int>(type: "int", nullable: false),
                    accion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    estadoAnterior = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    estadoNuevo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    detalle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    idUsuario = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    fechaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaOperacion", x => x.idAuditoria);
                });

            migrationBuilder.CreateTable(
                name: "MenuSistema",
                columns: table => new
                {
                    idMenu = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    controllerNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    orden = table.Column<int>(type: "int", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuSistema", x => x.idMenu);
                });

            migrationBuilder.CreateTable(
                name: "Venta",
                columns: table => new
                {
                    idVenta = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idCotizacion = table.Column<int>(type: "int", nullable: false),
                    idCliente = table.Column<int>(type: "int", nullable: false),
                    idUsuario = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    fechaVenta = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    total = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venta", x => x.idVenta);
                    table.ForeignKey(
                        name: "FK_Venta_AspNetUsers_idUsuario",
                        column: x => x.idUsuario,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Venta_Cliente_idCliente",
                        column: x => x.idCliente,
                        principalTable: "Cliente",
                        principalColumn: "idCliente",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Venta_Cotizacion_idCotizacion",
                        column: x => x.idCotizacion,
                        principalTable: "Cotizacion",
                        principalColumn: "idCotizacion",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PermisoRolMenu",
                columns: table => new
                {
                    idPermiso = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idMenu = table.Column<int>(type: "int", nullable: false),
                    idRol = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ver = table.Column<bool>(type: "bit", nullable: false),
                    crear = table.Column<bool>(type: "bit", nullable: false),
                    editar = table.Column<bool>(type: "bit", nullable: false),
                    eliminar = table.Column<bool>(type: "bit", nullable: false),
                    procesar = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisoRolMenu", x => x.idPermiso);
                    table.ForeignKey(
                        name: "FK_PermisoRolMenu_AspNetRoles_idRol",
                        column: x => x.idRol,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PermisoRolMenu_MenuSistema_idMenu",
                        column: x => x.idMenu,
                        principalTable: "MenuSistema",
                        principalColumn: "idMenu",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DetalleVenta",
                columns: table => new
                {
                    idDetalleVenta = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idVenta = table.Column<int>(type: "int", nullable: false),
                    idProducto = table.Column<int>(type: "int", nullable: false),
                    productoNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    cantidad = table.Column<int>(type: "int", nullable: false),
                    precioUnitario = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    esServicio = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetalleVenta", x => x.idDetalleVenta);
                    table.ForeignKey(
                        name: "FK_DetalleVenta_Producto_idProducto",
                        column: x => x.idProducto,
                        principalTable: "Producto",
                        principalColumn: "idProducto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetalleVenta_Venta_idVenta",
                        column: x => x.idVenta,
                        principalTable: "Venta",
                        principalColumn: "idVenta",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pedido_idVenta",
                table: "Pedido",
                column: "idVenta",
                unique: true,
                filter: "[idVenta] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaOperacion_tipoEntidad_idEntidad_fechaUtc",
                table: "AuditoriaOperacion",
                columns: new[] { "tipoEntidad", "idEntidad", "fechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DetalleVenta_idProducto",
                table: "DetalleVenta",
                column: "idProducto");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleVenta_idVenta",
                table: "DetalleVenta",
                column: "idVenta");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSistema_codigo",
                table: "MenuSistema",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermisoRolMenu_idMenu",
                table: "PermisoRolMenu",
                column: "idMenu");

            migrationBuilder.CreateIndex(
                name: "IX_PermisoRolMenu_idRol_idMenu",
                table: "PermisoRolMenu",
                columns: new[] { "idRol", "idMenu" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Venta_idCliente",
                table: "Venta",
                column: "idCliente");

            migrationBuilder.CreateIndex(
                name: "IX_Venta_idCotizacion",
                table: "Venta",
                column: "idCotizacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Venta_idUsuario",
                table: "Venta",
                column: "idUsuario");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallePedido_Producto_idProducto",
                table: "DetallePedido",
                column: "idProducto",
                principalTable: "Producto",
                principalColumn: "idProducto",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pedido_Venta_idVenta",
                table: "Pedido",
                column: "idVenta",
                principalTable: "Venta",
                principalColumn: "idVenta",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.InsertData(
                table: "MenuSistema",
                columns: new[] { "idMenu", "activo", "codigo", "controllerNombre", "nombre", "orden" },
                values: new object[,]
                {
                    { 1, true, "clientes", "Clientes", "Clientes", 10 },
                    { 2, true, "proveedores", "Proveedors", "Proveedores", 20 },
                    { 3, true, "productos", "Productoes", "Productos", 30 },
                    { 4, true, "cotizaciones", "Cotizacions", "Cotizaciones", 40 },
                    { 5, true, "ventas", "Ventas", "Ventas", 50 },
                    { 6, true, "taller", "Taller", "Taller", 60 }
                });

            migrationBuilder.Sql("""
                DECLARE @VendedorId nvarchar(450);
                DECLARE @TallerId nvarchar(450);

                SELECT @VendedorId = Id FROM AspNetRoles WHERE NormalizedName = N'VENDEDOR';
                IF @VendedorId IS NULL
                BEGIN
                    SET @VendedorId = CONVERT(nvarchar(450), NEWID());
                    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
                    VALUES (@VendedorId, N'Vendedor', N'VENDEDOR', CONVERT(nvarchar(36), NEWID()));
                END;

                SELECT @TallerId = Id FROM AspNetRoles WHERE NormalizedName = N'OPERADOR DE TALLER';
                IF @TallerId IS NULL
                BEGIN
                    SET @TallerId = CONVERT(nvarchar(450), NEWID());
                    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
                    VALUES (@TallerId, N'Operador de Taller', N'OPERADOR DE TALLER', CONVERT(nvarchar(36), NEWID()));
                END;

                INSERT INTO PermisoRolMenu (idRol, idMenu, ver, crear, editar, eliminar, procesar)
                SELECT @VendedorId, idMenu,
                    CASE WHEN codigo IN (N'clientes', N'productos', N'cotizaciones', N'ventas', N'taller') THEN 1 ELSE 0 END,
                    CASE WHEN codigo IN (N'clientes', N'cotizaciones') THEN 1 ELSE 0 END,
                    CASE WHEN codigo IN (N'clientes', N'cotizaciones') THEN 1 ELSE 0 END,
                    0,
                    CASE WHEN codigo = N'ventas' THEN 1 ELSE 0 END
                FROM MenuSistema;

                INSERT INTO PermisoRolMenu (idRol, idMenu, ver, crear, editar, eliminar, procesar)
                SELECT @TallerId, idMenu,
                    CASE WHEN codigo = N'taller' THEN 1 ELSE 0 END,
                    0, 0, 0,
                    CASE WHEN codigo = N'taller' THEN 1 ELSE 0 END
                FROM MenuSistema;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallePedido_Producto_idProducto",
                table: "DetallePedido");

            migrationBuilder.DropForeignKey(
                name: "FK_Pedido_Venta_idVenta",
                table: "Pedido");

            migrationBuilder.DropTable(
                name: "AuditoriaOperacion");

            migrationBuilder.DropTable(
                name: "DetalleVenta");

            migrationBuilder.DropTable(
                name: "PermisoRolMenu");

            migrationBuilder.DropTable(
                name: "Venta");

            migrationBuilder.DropTable(
                name: "MenuSistema");

            migrationBuilder.DropIndex(
                name: "IX_Pedido_idVenta",
                table: "Pedido");

            migrationBuilder.DropColumn(
                name: "esServicio",
                table: "Producto");

            migrationBuilder.DropColumn(
                name: "idVenta",
                table: "Pedido");

            migrationBuilder.DropColumn(
                name: "esServicio",
                table: "DetallePedido");

            migrationBuilder.DropColumn(
                name: "productoNombre",
                table: "DetallePedido");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "Cotizacion");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallePedido_Producto_idProducto",
                table: "DetallePedido",
                column: "idProducto",
                principalTable: "Producto",
                principalColumn: "idProducto");
        }
    }
}
