using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class InventorySchemaFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "idUnidadMedidaBase",
                table: "Producto",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Almacen",
                columns: table => new
                {
                    idAlmacen = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Almacen", x => x.idAlmacen);
                });

            migrationBuilder.CreateTable(
                name: "UnidadMedida",
                columns: table => new
                {
                    idUnidadMedida = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    descripcion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    descripcionCorta = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    codigoSunat = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidadMedida", x => x.idUnidadMedida);
                });

            migrationBuilder.CreateTable(
                name: "CorteInventario",
                columns: table => new
                {
                    idCorteInventario = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idAlmacen = table.Column<int>(type: "int", nullable: false),
                    fechaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    idUsuario = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorteInventario", x => x.idCorteInventario);
                    table.ForeignKey(
                        name: "FK_CorteInventario_Almacen_idAlmacen",
                        column: x => x.idAlmacen,
                        principalTable: "Almacen",
                        principalColumn: "idAlmacen",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Kardex",
                columns: table => new
                {
                    idKardex = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idAlmacen = table.Column<int>(type: "int", nullable: false),
                    idProducto = table.Column<int>(type: "int", nullable: false),
                    fechaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    tipoMovimiento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    cantidadBase = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    saldoAnteriorBase = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    saldoPosteriorBase = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    tipoReferencia = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    idReferencia = table.Column<int>(type: "int", nullable: true),
                    idUsuario = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kardex", x => x.idKardex);
                    table.CheckConstraint("CK_Kardex_Cantidad", "[cantidadBase] > 0");
                    table.CheckConstraint("CK_Kardex_Saldos", "[saldoAnteriorBase] >= 0 AND [saldoPosteriorBase] >= 0");
                    table.ForeignKey(
                        name: "FK_Kardex_Almacen_idAlmacen",
                        column: x => x.idAlmacen,
                        principalTable: "Almacen",
                        principalColumn: "idAlmacen",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Kardex_Producto_idProducto",
                        column: x => x.idProducto,
                        principalTable: "Producto",
                        principalColumn: "idProducto",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Stock",
                columns: table => new
                {
                    idAlmacen = table.Column<int>(type: "int", nullable: false),
                    idProducto = table.Column<int>(type: "int", nullable: false),
                    cantidadBase = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    rowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stock", x => new { x.idAlmacen, x.idProducto });
                    table.CheckConstraint("CK_Stock_CantidadBase", "[cantidadBase] >= 0");
                    table.ForeignKey(
                        name: "FK_Stock_Almacen_idAlmacen",
                        column: x => x.idAlmacen,
                        principalTable: "Almacen",
                        principalColumn: "idAlmacen",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Stock_Producto_idProducto",
                        column: x => x.idProducto,
                        principalTable: "Producto",
                        principalColumn: "idProducto",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PresentacionProducto",
                columns: table => new
                {
                    idPresentacionProducto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idProducto = table.Column<int>(type: "int", nullable: false),
                    idUnidadMedida = table.Column<int>(type: "int", nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    factor = table.Column<decimal>(type: "decimal(12,3)", nullable: false),
                    codigoBarras = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresentacionProducto", x => x.idPresentacionProducto);
                    table.CheckConstraint("CK_PresentacionProducto_Factor", "[factor] > 0");
                    table.ForeignKey(
                        name: "FK_PresentacionProducto_Producto_idProducto",
                        column: x => x.idProducto,
                        principalTable: "Producto",
                        principalColumn: "idProducto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PresentacionProducto_UnidadMedida_idUnidadMedida",
                        column: x => x.idUnidadMedida,
                        principalTable: "UnidadMedida",
                        principalColumn: "idUnidadMedida",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorteInventarioDetalle",
                columns: table => new
                {
                    idCorteInventarioDetalle = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idCorteInventario = table.Column<int>(type: "int", nullable: false),
                    idProducto = table.Column<int>(type: "int", nullable: false),
                    cantidadSistemaBase = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    cantidadContadaBase = table.Column<decimal>(type: "decimal(18,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorteInventarioDetalle", x => x.idCorteInventarioDetalle);
                    table.CheckConstraint("CK_CorteDetalle_Contada", "[cantidadContadaBase] >= 0");
                    table.CheckConstraint("CK_CorteDetalle_Sistema", "[cantidadSistemaBase] IS NULL OR [cantidadSistemaBase] >= 0");
                    table.ForeignKey(
                        name: "FK_CorteInventarioDetalle_CorteInventario_idCorteInventario",
                        column: x => x.idCorteInventario,
                        principalTable: "CorteInventario",
                        principalColumn: "idCorteInventario",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorteInventarioDetalle_Producto_idProducto",
                        column: x => x.idProducto,
                        principalTable: "Producto",
                        principalColumn: "idProducto",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Producto_idUnidadMedidaBase",
                table: "Producto",
                column: "idUnidadMedidaBase");

            migrationBuilder.CreateIndex(
                name: "IX_Almacen_nombre",
                table: "Almacen",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorteInventario_idAlmacen",
                table: "CorteInventario",
                column: "idAlmacen");

            migrationBuilder.CreateIndex(
                name: "IX_CorteInventarioDetalle_idCorteInventario_idProducto",
                table: "CorteInventarioDetalle",
                columns: new[] { "idCorteInventario", "idProducto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorteInventarioDetalle_idProducto",
                table: "CorteInventarioDetalle",
                column: "idProducto");

            migrationBuilder.CreateIndex(
                name: "IX_Kardex_idAlmacen_idProducto_fechaUtc",
                table: "Kardex",
                columns: new[] { "idAlmacen", "idProducto", "fechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Kardex_idProducto",
                table: "Kardex",
                column: "idProducto");

            migrationBuilder.CreateIndex(
                name: "IX_PresentacionProducto_idProducto",
                table: "PresentacionProducto",
                column: "idProducto");

            migrationBuilder.CreateIndex(
                name: "IX_PresentacionProducto_idUnidadMedida",
                table: "PresentacionProducto",
                column: "idUnidadMedida");

            migrationBuilder.CreateIndex(
                name: "IX_Stock_idProducto",
                table: "Stock",
                column: "idProducto");

            migrationBuilder.AddForeignKey(
                name: "FK_Producto_UnidadMedida_idUnidadMedidaBase",
                table: "Producto",
                column: "idUnidadMedidaBase",
                principalTable: "UnidadMedida",
                principalColumn: "idUnidadMedida",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Producto_UnidadMedida_idUnidadMedidaBase",
                table: "Producto");

            migrationBuilder.DropTable(
                name: "CorteInventarioDetalle");

            migrationBuilder.DropTable(
                name: "Kardex");

            migrationBuilder.DropTable(
                name: "PresentacionProducto");

            migrationBuilder.DropTable(
                name: "Stock");

            migrationBuilder.DropTable(
                name: "CorteInventario");

            migrationBuilder.DropTable(
                name: "UnidadMedida");

            migrationBuilder.DropTable(
                name: "Almacen");

            migrationBuilder.DropIndex(
                name: "IX_Producto_idUnidadMedidaBase",
                table: "Producto");

            migrationBuilder.DropColumn(
                name: "idUnidadMedidaBase",
                table: "Producto");
        }
    }
}
