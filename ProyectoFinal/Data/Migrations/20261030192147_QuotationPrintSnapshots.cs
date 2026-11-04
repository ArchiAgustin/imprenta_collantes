using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuotationPrintSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "productoNombreEmision",
                table: "DetalleCotizacion",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "clienteDireccionEmision",
                table: "Cotizacion",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "clienteDocumentoEmision",
                table: "Cotizacion",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "clienteNombreEmision",
                table: "Cotizacion",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "productoNombreEmision",
                table: "DetalleCotizacion");

            migrationBuilder.DropColumn(
                name: "clienteDireccionEmision",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "clienteDocumentoEmision",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "clienteNombreEmision",
                table: "Cotizacion");
        }
    }
}
