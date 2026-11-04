using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableInternalSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "digitosNumeroInterno",
                table: "Venta",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "digitosNumeroInterno",
                table: "Cotizacion",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SerieDocumentoInterno",
                columns: table => new
                {
                    idSerieDocumentoInterno = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tipoDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    serie = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    siguienteCorrelativo = table.Column<int>(type: "int", nullable: false),
                    digitos = table.Column<int>(type: "int", nullable: false),
                    activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SerieDocumentoInterno", x => x.idSerieDocumentoInterno);
                    table.CheckConstraint("CK_SerieInterna_Digitos", "[digitos] BETWEEN 4 AND 10");
                    table.CheckConstraint("CK_SerieInterna_Siguiente", "[siguienteCorrelativo] > 0");
                    table.CheckConstraint("CK_SerieInterna_Tipo", "[tipoDocumento] IN (N'Cotizacion', N'Venta')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SerieDocumentoInterno_tipoDocumento",
                table: "SerieDocumentoInterno",
                column: "tipoDocumento",
                unique: true,
                filter: "[activa] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SerieDocumentoInterno_tipoDocumento_serie",
                table: "SerieDocumentoInterno",
                columns: new[] { "tipoDocumento", "serie" },
                unique: true);

            // Never rewrite already issued CI/VI documents. A different database
            // with C001/V001 already used must start after its actual maximum.
            migrationBuilder.Sql(@"
INSERT INTO dbo.SerieDocumentoInterno (tipoDocumento, serie, siguienteCorrelativo, digitos, activa)
SELECT N'Cotizacion', N'C001',
       CASE WHEN ISNULL(MAX(correlativoInterno), 4531) < 4531 THEN 4532
            ELSE ISNULL(MAX(correlativoInterno), 4531) + 1 END, 6, 1
FROM dbo.Cotizacion WHERE serieInterna = N'C001';
INSERT INTO dbo.SerieDocumentoInterno (tipoDocumento, serie, siguienteCorrelativo, digitos, activa)
SELECT N'Venta', N'V001',
       CASE WHEN ISNULL(MAX(correlativoInterno), 4531) < 4531 THEN 4532
            ELSE ISNULL(MAX(correlativoInterno), 4531) + 1 END, 6, 1
FROM dbo.Venta WHERE serieInterna = N'V001';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SerieDocumentoInterno");

            migrationBuilder.DropColumn(
                name: "digitosNumeroInterno",
                table: "Venta");

            migrationBuilder.DropColumn(
                name: "digitosNumeroInterno",
                table: "Cotizacion");
        }
    }
}
