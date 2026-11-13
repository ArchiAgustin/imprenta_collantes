using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations;

[DbContext(typeof(ProyectoFinal.Data.ApplicationDbContext))]
[Migration("20261106100000_QuoteLinePresentationSnapshots")]
public partial class QuoteLinePresentationSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddLinePresentationColumns(migrationBuilder, "DetalleCotizacion");
        AddLinePresentationColumns(migrationBuilder, "DetalleVenta");
        AddLinePresentationColumns(migrationBuilder, "DetallePedido");

        migrationBuilder.CreateIndex(name: "IX_DetalleCotizacion_idPresentacionProducto", table: "DetalleCotizacion", column: "idPresentacionProducto");
        migrationBuilder.CreateIndex(name: "IX_DetalleVenta_idPresentacionProducto", table: "DetalleVenta", column: "idPresentacionProducto");
        migrationBuilder.CreateIndex(name: "IX_DetallePedido_idPresentacionProducto", table: "DetallePedido", column: "idPresentacionProducto");
        migrationBuilder.AddForeignKey(
            name: "FK_DetalleCotizacion_PresentacionProducto_idPresentacionProducto",
            table: "DetalleCotizacion", column: "idPresentacionProducto",
            principalTable: "PresentacionProducto", principalColumn: "idPresentacionProducto",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_DetalleVenta_PresentacionProducto_idPresentacionProducto",
            table: "DetalleVenta", column: "idPresentacionProducto",
            principalTable: "PresentacionProducto", principalColumn: "idPresentacionProducto",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_DetallePedido_PresentacionProducto_idPresentacionProducto",
            table: "DetallePedido", column: "idPresentacionProducto",
            principalTable: "PresentacionProducto", principalColumn: "idPresentacionProducto",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_DetalleCotizacion_PresentacionProducto_idPresentacionProducto", table: "DetalleCotizacion");
        migrationBuilder.DropForeignKey(name: "FK_DetalleVenta_PresentacionProducto_idPresentacionProducto", table: "DetalleVenta");
        migrationBuilder.DropForeignKey(name: "FK_DetallePedido_PresentacionProducto_idPresentacionProducto", table: "DetallePedido");
        migrationBuilder.DropIndex(name: "IX_DetalleCotizacion_idPresentacionProducto", table: "DetalleCotizacion");
        migrationBuilder.DropIndex(name: "IX_DetalleVenta_idPresentacionProducto", table: "DetalleVenta");
        migrationBuilder.DropIndex(name: "IX_DetallePedido_idPresentacionProducto", table: "DetallePedido");
        DropLinePresentationColumns(migrationBuilder, "DetalleCotizacion");
        DropLinePresentationColumns(migrationBuilder, "DetalleVenta");
        DropLinePresentationColumns(migrationBuilder, "DetallePedido");
    }

    private static void AddLinePresentationColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<int>(name: "idPresentacionProducto", table: table, type: "int", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "factorPresentacion", table: table, type: "decimal(12,3)", nullable: false, defaultValue: 1m);
        migrationBuilder.AddColumn<string>(name: "presentacionDescripcion", table: table, type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "unidadBaseDescripcion", table: table, type: "nvarchar(100)", maxLength: 100, nullable: true);
    }

    private static void DropLinePresentationColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropColumn(name: "idPresentacionProducto", table: table);
        migrationBuilder.DropColumn(name: "factorPresentacion", table: table);
        migrationBuilder.DropColumn(name: "presentacionDescripcion", table: table);
        migrationBuilder.DropColumn(name: "unidadBaseDescripcion", table: table);
    }
}
