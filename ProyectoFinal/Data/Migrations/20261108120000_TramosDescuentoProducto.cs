using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ProyectoFinal.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261108120000_TramosDescuentoProducto")]
public sealed class TramosDescuentoProducto : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TramoDescuentoProducto",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                IdProducto = table.Column<int>(nullable: false),
                CantidadMinima = table.Column<int>(nullable: false),
                DescuentoPorcentaje = table.Column<decimal>(type: "decimal(5,2)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TramoDescuentoProducto", x => x.Id);
                table.ForeignKey("FK_TramoDescuentoProducto_Producto_IdProducto", x => x.IdProducto,
                    "Producto", "idProducto", onDelete: ReferentialAction.Cascade);
                table.CheckConstraint("CK_TramoDescuento_Cantidad", "[CantidadMinima] BETWEEN 1 AND 1000000");
                table.CheckConstraint("CK_TramoDescuento_Porcentaje", "[DescuentoPorcentaje] BETWEEN 0 AND 99.99");
            });
        migrationBuilder.CreateIndex("IX_TramoDescuentoProducto_IdProducto_CantidadMinima",
            "TramoDescuentoProducto", new[] { "IdProducto", "CantidadMinima" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("TramoDescuentoProducto");
}
