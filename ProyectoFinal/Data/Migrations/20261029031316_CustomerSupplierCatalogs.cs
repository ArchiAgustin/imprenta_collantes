using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerSupplierCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "direccion",
                table: "Proveedor",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ruc",
                table: "Proveedor",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ubigeo",
                table: "Proveedor",
                type: "char(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "idDocumentoIdentidadTipo",
                table: "Cliente",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numeroDocumento",
                table: "Cliente",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ubigeo",
                table: "Cliente",
                type: "char(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "documentoidentidad_tipo",
                columns: table => new
                {
                    iddocumentoidentidadtipo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    abreviatura = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    longitud = table.Column<byte>(type: "tinyint", nullable: true),
                    estado = table.Column<bool>(type: "bit", nullable: false),
                    eliminado = table.Column<bool>(type: "bit", nullable: false),
                    idusuario_crea = table.Column<int>(type: "int", nullable: true),
                    idusuario_modifica = table.Column<int>(type: "int", nullable: true),
                    idusuario_elimina = table.Column<int>(type: "int", nullable: true),
                    fecha_crea = table.Column<DateTime>(type: "datetime2", nullable: false),
                    fecha_modifica = table.Column<DateTime>(type: "datetime2", nullable: true),
                    fecha_elimina = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentoidentidad_tipo", x => x.iddocumentoidentidadtipo);
                });

            migrationBuilder.CreateTable(
                name: "ubigeo",
                columns: table => new
                {
                    idubigeo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ubigeo = table.Column<string>(type: "char(6)", maxLength: 6, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    parent = table.Column<string>(type: "char(6)", maxLength: 6, nullable: false),
                    idpais = table.Column<int>(type: "int", nullable: true),
                    idparent = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ubigeo", x => x.idubigeo);
                    table.UniqueConstraint("AK_ubigeo_ubigeo", x => x.ubigeo);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proveedor_ruc",
                table: "Proveedor",
                column: "ruc",
                unique: true,
                filter: "[ruc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Proveedor_ubigeo",
                table: "Proveedor",
                column: "ubigeo");

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_idDocumentoIdentidadTipo_numeroDocumento",
                table: "Cliente",
                columns: new[] { "idDocumentoIdentidadTipo", "numeroDocumento" },
                unique: true,
                filter: "[idDocumentoIdentidadTipo] IS NOT NULL AND [numeroDocumento] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_ubigeo",
                table: "Cliente",
                column: "ubigeo");

            migrationBuilder.CreateIndex(
                name: "IX_documentoidentidad_tipo_codigo",
                table: "documentoidentidad_tipo",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ubigeo_parent",
                table: "ubigeo",
                column: "parent");

            migrationBuilder.CreateIndex(
                name: "IX_ubigeo_ubigeo",
                table: "ubigeo",
                column: "ubigeo",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cliente_documentoidentidad_tipo_idDocumentoIdentidadTipo",
                table: "Cliente",
                column: "idDocumentoIdentidadTipo",
                principalTable: "documentoidentidad_tipo",
                principalColumn: "iddocumentoidentidadtipo",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cliente_ubigeo_ubigeo",
                table: "Cliente",
                column: "ubigeo",
                principalTable: "ubigeo",
                principalColumn: "ubigeo",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Proveedor_ubigeo_ubigeo",
                table: "Proveedor",
                column: "ubigeo",
                principalTable: "ubigeo",
                principalColumn: "ubigeo",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cliente_documentoidentidad_tipo_idDocumentoIdentidadTipo",
                table: "Cliente");

            migrationBuilder.DropForeignKey(
                name: "FK_Cliente_ubigeo_ubigeo",
                table: "Cliente");

            migrationBuilder.DropForeignKey(
                name: "FK_Proveedor_ubigeo_ubigeo",
                table: "Proveedor");

            migrationBuilder.DropTable(
                name: "documentoidentidad_tipo");

            migrationBuilder.DropTable(
                name: "ubigeo");

            migrationBuilder.DropIndex(
                name: "IX_Proveedor_ruc",
                table: "Proveedor");

            migrationBuilder.DropIndex(
                name: "IX_Proveedor_ubigeo",
                table: "Proveedor");

            migrationBuilder.DropIndex(
                name: "IX_Cliente_idDocumentoIdentidadTipo_numeroDocumento",
                table: "Cliente");

            migrationBuilder.DropIndex(
                name: "IX_Cliente_ubigeo",
                table: "Cliente");

            migrationBuilder.DropColumn(
                name: "direccion",
                table: "Proveedor");

            migrationBuilder.DropColumn(
                name: "ruc",
                table: "Proveedor");

            migrationBuilder.DropColumn(
                name: "ubigeo",
                table: "Proveedor");

            migrationBuilder.DropColumn(
                name: "idDocumentoIdentidadTipo",
                table: "Cliente");

            migrationBuilder.DropColumn(
                name: "numeroDocumento",
                table: "Cliente");

            migrationBuilder.DropColumn(
                name: "ubigeo",
                table: "Cliente");
        }
    }
}
