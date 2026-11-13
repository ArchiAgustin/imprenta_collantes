using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations;

[DbContext(typeof(ProyectoFinal.Data.ApplicationDbContext))]
[Migration("20261105100000_UnifiedSupplierIdentity")]
public partial class UnifiedSupplierIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "idDocumentoIdentidadTipo", table: "Proveedor", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "numeroDocumento", table: "Proveedor", type: "nvarchar(20)", maxLength: 20, nullable: true);

        migrationBuilder.Sql("""
            UPDATE p
            SET p.idDocumentoIdentidadTipo = d.iddocumentoidentidadtipo,
                p.numeroDocumento = p.ruc
            FROM dbo.Proveedor AS p
            INNER JOIN dbo.documentoidentidad_tipo AS d ON d.codigo = N'6'
            WHERE p.ruc IS NOT NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Proveedor_idDocumentoIdentidadTipo_numeroDocumento",
            table: "Proveedor",
            columns: new[] { "idDocumentoIdentidadTipo", "numeroDocumento" },
            unique: true,
            filter: "[idDocumentoIdentidadTipo] IS NOT NULL AND [numeroDocumento] IS NOT NULL");
        migrationBuilder.AddForeignKey(
            name: "FK_Proveedor_documentoidentidad_tipo_idDocumentoIdentidadTipo",
            table: "Proveedor",
            column: "idDocumentoIdentidadTipo",
            principalTable: "documentoidentidad_tipo",
            principalColumn: "iddocumentoidentidadtipo",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Proveedor_documentoidentidad_tipo_idDocumentoIdentidadTipo", table: "Proveedor");
        migrationBuilder.DropIndex(name: "IX_Proveedor_idDocumentoIdentidadTipo_numeroDocumento", table: "Proveedor");
        migrationBuilder.DropColumn(name: "idDocumentoIdentidadTipo", table: "Proveedor");
        migrationBuilder.DropColumn(name: "numeroDocumento", table: "Proveedor");
    }
}
