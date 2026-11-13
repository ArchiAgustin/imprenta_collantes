using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations;

[DbContext(typeof(ProyectoFinal.Data.ApplicationDbContext))]
[Migration("20261105120000_EmailConfigurationParametersMenu")]
public partial class EmailConfigurationParametersMenu : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ConfiguracionCorreo",
            columns: table => new
            {
                id = table.Column<int>(type: "int", nullable: false),
                servidor = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                puerto = table.Column<int>(type: "int", nullable: false),
                correoRemitente = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                nombreRemitente = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                usuario = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                contrasenaProtegida = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                usarTls = table.Column<bool>(type: "bit", nullable: false),
                actualizadoUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConfiguracionCorreo", x => x.id);
                table.CheckConstraint("CK_ConfiguracionCorreo_Id", "[id] = 1");
                table.CheckConstraint("CK_ConfiguracionCorreo_Puerto", "[puerto] BETWEEN 1 AND 65535");
            });

        migrationBuilder.Sql("""
            DECLARE @ConfiguracionId int = (SELECT TOP (1) idMenu FROM dbo.MenuSistema WHERE codigo = N'configuracion');
            IF @ConfiguracionId IS NOT NULL
            BEGIN
                IF EXISTS (SELECT 1 FROM dbo.MenuSistema WHERE codigo = N'parametros')
                    UPDATE dbo.MenuSistema SET nombre = N'Parámetros', idMenuPadre = @ConfiguracionId,
                        icono = N'sliders', destino = N'parametros', controllerNombre = N'Parametros', orden = 30, activo = 1
                    WHERE codigo = N'parametros';
                ELSE
                    INSERT dbo.MenuSistema (codigo, nombre, idMenuPadre, icono, destino, controllerNombre, orden, activo)
                    VALUES (N'parametros', N'Parámetros', @ConfiguracionId, N'sliders', N'parametros', N'Parametros', 30, 1);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE permission FROM dbo.PermisoRolMenu AS permission
            INNER JOIN dbo.MenuSistema AS menu ON menu.idMenu = permission.idMenu
            WHERE menu.codigo = N'parametros';
            DELETE FROM dbo.MenuSistema WHERE codigo = N'parametros';
            """);
        migrationBuilder.DropTable(name: "ConfiguracionCorreo");
    }
}
