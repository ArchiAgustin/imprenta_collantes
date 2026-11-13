using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProyectoFinal.Data;

namespace ProyectoFinal.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261104120000_MultiWarehouseOperations")]
public sealed class MultiWarehouseOperations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "predeterminado", table: "Almacen", type: "bit", nullable: false,
            defaultValue: false);
        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM dbo.Almacen WHERE predeterminado = 1)
BEGIN
    UPDATE dbo.Almacen SET predeterminado = 1
    WHERE idAlmacen = (SELECT TOP (1) idAlmacen FROM dbo.Almacen
                       WHERE nombre = N'Almacén principal' ORDER BY idAlmacen);
END;
IF (SELECT COUNT(*) FROM dbo.Almacen WHERE predeterminado = 1) > 1
    THROW 51000, 'Solo puede existir un almacén predeterminado.', 1;");
        migrationBuilder.CreateIndex(
            name: "IX_Almacen_predeterminado", table: "Almacen", column: "predeterminado",
            unique: true, filter: "[predeterminado] = 1");

        migrationBuilder.AddColumn<int>(name: "idAlmacen", table: "Venta", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>(name: "almacenNombreEmision", table: "Venta", type: "nvarchar(150)", maxLength: 150, nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Venta_idAlmacen", table: "Venta", column: "idAlmacen");
        migrationBuilder.AddForeignKey(name: "FK_Venta_Almacen_idAlmacen", table: "Venta", column: "idAlmacen",
            principalTable: "Almacen", principalColumn: "idAlmacen", onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateTable(
            name: "TransferenciaAlmacen",
            columns: table => new
            {
                idTransferenciaAlmacen = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                idAlmacenOrigen = table.Column<int>(type: "int", nullable: false),
                idAlmacenDestino = table.Column<int>(type: "int", nullable: false),
                idProducto = table.Column<int>(type: "int", nullable: false),
                cantidadBase = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                fechaUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                idUsuario = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransferenciaAlmacen", x => x.idTransferenciaAlmacen);
                table.CheckConstraint("CK_TransferenciaAlmacen_Almacenes", "[idAlmacenOrigen] <> [idAlmacenDestino]");
                table.ForeignKey("FK_TransferenciaAlmacen_Almacen_idAlmacenOrigen", x => x.idAlmacenOrigen, "Almacen", "idAlmacen", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_TransferenciaAlmacen_Almacen_idAlmacenDestino", x => x.idAlmacenDestino, "Almacen", "idAlmacen", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_TransferenciaAlmacen_Producto_idProducto", x => x.idProducto, "Producto", "idProducto", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_TransferenciaAlmacen_idAlmacenOrigen_fechaUtc", table: "TransferenciaAlmacen", columns: new[] { "idAlmacenOrigen", "fechaUtc" });
        migrationBuilder.CreateIndex(name: "IX_TransferenciaAlmacen_idAlmacenDestino_fechaUtc", table: "TransferenciaAlmacen", columns: new[] { "idAlmacenDestino", "fechaUtc" });
        migrationBuilder.CreateIndex(name: "IX_TransferenciaAlmacen_idProducto", table: "TransferenciaAlmacen", column: "idProducto");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TransferenciaAlmacen");
        migrationBuilder.DropForeignKey(name: "FK_Venta_Almacen_idAlmacen", table: "Venta");
        migrationBuilder.DropIndex(name: "IX_Venta_idAlmacen", table: "Venta");
        migrationBuilder.DropColumn(name: "idAlmacen", table: "Venta");
        migrationBuilder.DropColumn(name: "almacenNombreEmision", table: "Venta");
        migrationBuilder.DropIndex(name: "IX_Almacen_predeterminado", table: "Almacen");
        migrationBuilder.DropColumn(name: "predeterminado", table: "Almacen");
    }
}
