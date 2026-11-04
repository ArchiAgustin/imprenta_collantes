using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class InternalCommercialDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "clienteDireccionEmision",
                table: "Venta",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "clienteDocumentoEmision",
                table: "Venta",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "clienteNombreEmision",
                table: "Venta",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "correlativoInterno",
                table: "Venta",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "igvEmision",
                table: "Venta",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "serieInterna",
                table: "Venta",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "subtotalSinIgvEmision",
                table: "Venta",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "correlativoInterno",
                table: "Cotizacion",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "idCotizacionOrigen",
                table: "Cotizacion",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "serieInterna",
                table: "Cotizacion",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Venta_serieInterna_correlativoInterno",
                table: "Venta",
                columns: new[] { "serieInterna", "correlativoInterno" },
                unique: true,
                filter: "[serieInterna] IS NOT NULL AND [correlativoInterno] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_idCotizacionOrigen",
                table: "Cotizacion",
                column: "idCotizacionOrigen");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_serieInterna_correlativoInterno",
                table: "Cotizacion",
                columns: new[] { "serieInterna", "correlativoInterno" },
                unique: true,
                filter: "[serieInterna] IS NOT NULL AND [correlativoInterno] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Cotizacion_Cotizacion_idCotizacionOrigen",
                table: "Cotizacion",
                column: "idCotizacionOrigen",
                principalTable: "Cotizacion",
                principalColumn: "idCotizacion",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cotizacion_Cotizacion_idCotizacionOrigen",
                table: "Cotizacion");

            migrationBuilder.DropIndex(
                name: "IX_Venta_serieInterna_correlativoInterno",
                table: "Venta");

            migrationBuilder.DropIndex(
                name: "IX_Cotizacion_idCotizacionOrigen",
                table: "Cotizacion");

            migrationBuilder.DropIndex(
                name: "IX_Cotizacion_serieInterna_correlativoInterno",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "clienteDireccionEmision",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "clienteDocumentoEmision",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "clienteNombreEmision",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "correlativoInterno",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "igvEmision",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "serieInterna",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "subtotalSinIgvEmision",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "correlativoInterno",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "idCotizacionOrigen",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "serieInterna",
                table: "Cotizacion");
        }
    }
}
