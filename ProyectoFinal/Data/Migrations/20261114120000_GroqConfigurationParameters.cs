using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations;

[DbContext(typeof(ProyectoFinal.Data.ApplicationDbContext))]
[Migration("20261114120000_GroqConfigurationParameters")]
public partial class GroqConfigurationParameters : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ConfiguracionGroq",
            columns: table => new
            {
                id = table.Column<int>(type: "int", nullable: false),
                apiKeyProtegida = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                actualizadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConfiguracionGroq", x => x.id);
                table.CheckConstraint("CK_ConfiguracionGroq_Id", "[id] = 1");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "ConfiguracionGroq");
}
