using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProyectoFinal.Data;

namespace ProyectoFinal.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261104200000_InventoryHeadersDetailsAndSeries")]
public sealed class InventoryHeadersDetailsAndSeries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SerieInventario",
            columns: table => new
            {
                idSerieInventario = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                serie = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                siguienteCorrelativo = table.Column<int>(type: "int", nullable: false),
                digitos = table.Column<int>(type: "int", nullable: false),
                activa = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SerieInventario", x => x.idSerieInventario);
                table.CheckConstraint("CK_SerieInventario_Correlativo", "[siguienteCorrelativo] BETWEEN 1 AND 99999999");
                table.CheckConstraint("CK_SerieInventario_Digitos", "[digitos] = 8");
            });

        migrationBuilder.CreateTable(
            name: "Inventario",
            columns: table => new
            {
                idInventario = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                serie = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                correlativo = table.Column<int>(type: "int", nullable: false),
                fecha = table.Column<DateOnly>(type: "date", nullable: false),
                creadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                idAlmacen = table.Column<int>(type: "int", nullable: false),
                idUsuario = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                idCorteInventarioOrigen = table.Column<int>(type: "int", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Inventario", x => x.idInventario);
                table.ForeignKey("FK_Inventario_Almacen_IdAlmacen", x => x.idAlmacen, "Almacen", "idAlmacen", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Inventario_CorteInventario_IdCorteInventarioOrigen", x => x.idCorteInventarioOrigen,
                    "CorteInventario", "idCorteInventario", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_Inventario_Correlativo", "[correlativo] BETWEEN 1 AND 99999999");
            });

        migrationBuilder.CreateTable(
            name: "InventarioDetalle",
            columns: table => new
            {
                idInventarioDetalle = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                idInventario = table.Column<int>(type: "int", nullable: false),
                idProducto = table.Column<int>(type: "int", nullable: false),
                cantidadSistemaBase = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                cantidadContadaBase = table.Column<decimal>(type: "decimal(18,3)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InventarioDetalle", x => x.idInventarioDetalle);
                table.ForeignKey("FK_InventarioDetalle_Inventario_IdInventario", x => x.idInventario, "Inventario", "idInventario", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InventarioDetalle_Producto_IdProducto", x => x.idProducto, "Producto", "idProducto", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_InventarioDetalle_Cantidad", "[cantidadContadaBase] >= 0");
                table.CheckConstraint("CK_InventarioDetalle_Sistema", "[cantidadSistemaBase] IS NULL OR [cantidadSistemaBase] >= 0");
            });

        migrationBuilder.CreateIndex(name: "IX_SerieInventario_Serie", table: "SerieInventario", column: "serie", unique: true);
        migrationBuilder.CreateIndex(name: "IX_SerieInventario_Activa", table: "SerieInventario", column: "activa", unique: true, filter: "[activa] = 1");
        migrationBuilder.CreateIndex(name: "IX_Inventario_Serie_Correlativo", table: "Inventario", columns: new[] { "serie", "correlativo" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Inventario_IdAlmacen_Fecha", table: "Inventario", columns: new[] { "idAlmacen", "fecha" });
        migrationBuilder.CreateIndex(name: "IX_Inventario_IdCorteInventarioOrigen", table: "Inventario", column: "idCorteInventarioOrigen", unique: true, filter: "[idCorteInventarioOrigen] IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_InventarioDetalle_IdInventario_IdProducto", table: "InventarioDetalle", columns: new[] { "idInventario", "idProducto" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_InventarioDetalle_IdProducto", table: "InventarioDetalle", column: "idProducto");

        migrationBuilder.Sql("""
            DECLARE @LegacyMap TABLE (idCorteInventario int NOT NULL PRIMARY KEY, idInventario int NOT NULL);
            ;WITH CortesAprobados AS
            (
                SELECT idCorteInventario, idAlmacen, fechaUtc, idUsuario,
                    CONVERT(date, DATEADD(hour, -5, fechaUtc)) AS fecha,
                    ROW_NUMBER() OVER (ORDER BY fechaUtc, idCorteInventario) AS correlativo
                FROM dbo.CorteInventario
                WHERE estado = N'Aprobado'
            )
            INSERT dbo.Inventario (serie, correlativo, fecha, creadoUtc, idAlmacen, idUsuario, idCorteInventarioOrigen)
            OUTPUT inserted.idCorteInventarioOrigen, inserted.idInventario
                INTO @LegacyMap(idCorteInventario, idInventario)
            SELECT N'INV1', correlativo, fecha, fechaUtc, idAlmacen, idUsuario, idCorteInventario
            FROM CortesAprobados;

            INSERT dbo.InventarioDetalle (idInventario, idProducto, cantidadSistemaBase, cantidadContadaBase)
            SELECT map.idInventario, detalle.idProducto, detalle.cantidadSistemaBase, detalle.cantidadContadaBase
            FROM @LegacyMap AS map
            INNER JOIN dbo.CorteInventarioDetalle AS detalle
                ON detalle.idCorteInventario = map.idCorteInventario
            WHERE detalle.conteoRegistrado = 1;

            DECLARE @Siguiente int = ISNULL((SELECT MAX(correlativo) + 1 FROM dbo.Inventario WHERE serie = N'INV1'), 1);
            IF @Siguiente > 99999999
                THROW 51030, 'La serie INV1 excede su correlativo de ocho dígitos.', 1;
            INSERT dbo.SerieInventario (serie, siguienteCorrelativo, digitos, activa)
            VALUES (N'INV1', @Siguiente, 8, 1);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.Inventario WHERE idCorteInventarioOrigen IS NULL)
                THROW 51031, 'No se puede eliminar el historial de inventarios nuevos.', 1;
            """);
        migrationBuilder.DropTable(name: "InventarioDetalle");
        migrationBuilder.DropTable(name: "Inventario");
        migrationBuilder.DropTable(name: "SerieInventario");
    }
}
