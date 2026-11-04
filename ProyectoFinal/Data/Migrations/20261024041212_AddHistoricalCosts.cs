using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "costoUnitarioSinIgv",
                table: "Producto",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "costoUnitarioSinIgv",
                table: "DetalleVenta",
                type: "decimal(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "costoUnitarioSinIgv",
                table: "Producto");

            migrationBuilder.DropColumn(
                name: "costoUnitarioSinIgv",
                table: "DetalleVenta");
        }
    }
}
