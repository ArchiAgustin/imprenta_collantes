using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class SunatReferenceCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mae_detraccion",
                columns: table => new
                {
                    iddetraccion = table.Column<int>(type: "int", nullable: false),
                    codigo = table.Column<string>(type: "char(3)", maxLength: 3, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    porcentaje = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_detraccion", x => x.iddetraccion);
                });

            migrationBuilder.CreateTable(
                name: "mae_entidadfinanciera_tipo",
                columns: table => new
                {
                    identidadfinancieratipo = table.Column<int>(type: "int", nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    desc_corta = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_entidadfinanciera_tipo", x => x.identidadfinancieratipo);
                });

            migrationBuilder.CreateTable(
                name: "mae_moneda",
                columns: table => new
                {
                    idmoneda = table.Column<int>(type: "int", nullable: false),
                    codigosunat = table.Column<string>(type: "char(1)", maxLength: 1, nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    desc_corta = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    simbolo = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    codigoiso = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_moneda", x => x.idmoneda);
                });

            migrationBuilder.CreateTable(
                name: "mae_motivonc",
                columns: table => new
                {
                    idmotivonc = table.Column<int>(type: "int", nullable: false),
                    codigosunat = table.Column<string>(type: "char(2)", maxLength: 2, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_motivonc", x => x.idmotivonc);
                });

            migrationBuilder.CreateTable(
                name: "mae_motivond",
                columns: table => new
                {
                    idmotivond = table.Column<int>(type: "int", nullable: false),
                    codigosunat = table.Column<string>(type: "char(2)", maxLength: 2, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_motivond", x => x.idmotivond);
                });

            migrationBuilder.CreateTable(
                name: "mae_motivotraslado",
                columns: table => new
                {
                    idmotivotraslado = table.Column<int>(type: "int", nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    codigosunat = table.Column<string>(type: "char(2)", maxLength: 2, nullable: true),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_motivotraslado", x => x.idmotivotraslado);
                });

            migrationBuilder.CreateTable(
                name: "mae_tipoactividad",
                columns: table => new
                {
                    idtipoactividad = table.Column<int>(type: "int", nullable: false),
                    codigosunat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    desc_corta = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_tipoactividad", x => x.idtipoactividad);
                });

            migrationBuilder.CreateTable(
                name: "mae_tipocontribuyente",
                columns: table => new
                {
                    idtipocontribuyente = table.Column<int>(type: "int", nullable: false),
                    codigosunat = table.Column<string>(type: "char(2)", maxLength: 2, nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_tipocontribuyente", x => x.idtipocontribuyente);
                });

            migrationBuilder.CreateTable(
                name: "mae_tipodocumentopago",
                columns: table => new
                {
                    idtipodocumentopago = table.Column<int>(type: "int", nullable: false),
                    codigosunat = table.Column<string>(type: "char(3)", maxLength: 3, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    desc_corta = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    mostrar_rc = table.Column<bool>(type: "bit", nullable: true),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_tipodocumentopago", x => x.idtipodocumentopago);
                });

            migrationBuilder.CreateTable(
                name: "mae_tipomediopago",
                columns: table => new
                {
                    idtipomediopago = table.Column<int>(type: "int", nullable: false),
                    codigosunat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    desc_corta = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    monederodigital = table.Column<bool>(type: "bit", nullable: true),
                    cuentainterna = table.Column<bool>(type: "bit", nullable: true),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_tipomediopago", x => x.idtipomediopago);
                });

            migrationBuilder.CreateTable(
                name: "mae_via_tipo",
                columns: table => new
                {
                    idviatipo = table.Column<int>(type: "int", nullable: false),
                    codigosunat = table.Column<string>(type: "char(2)", maxLength: 2, nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    desc_corta = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_via_tipo", x => x.idviatipo);
                });

            migrationBuilder.CreateTable(
                name: "mae_zona_tipo",
                columns: table => new
                {
                    idzonatipo = table.Column<int>(type: "int", nullable: false),
                    codigosunat = table.Column<string>(type: "char(2)", maxLength: 2, nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    desc_corta = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_zona_tipo", x => x.idzonatipo);
                });

            migrationBuilder.CreateTable(
                name: "ven_estadosunat",
                columns: table => new
                {
                    idestadosunat = table.Column<int>(type: "int", nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ven_estadosunat", x => x.idestadosunat);
                });

            migrationBuilder.CreateTable(
                name: "mae_entidadfinanciera",
                columns: table => new
                {
                    identidadfinanciera = table.Column<int>(type: "int", nullable: false),
                    identidadfinancieratipo = table.Column<int>(type: "int", nullable: false),
                    codigosunat = table.Column<string>(type: "char(2)", maxLength: 2, nullable: true),
                    descripcion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    desc_corta = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mae_entidadfinanciera", x => x.identidadfinanciera);
                    table.ForeignKey(
                        name: "FK_mae_entidadfinanciera_mae_entidadfinanciera_tipo_identidadfinancieratipo",
                        column: x => x.identidadfinancieratipo,
                        principalTable: "mae_entidadfinanciera_tipo",
                        principalColumn: "identidadfinancieratipo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mae_entidadfinanciera_identidadfinancieratipo",
                table: "mae_entidadfinanciera",
                column: "identidadfinancieratipo");

            migrationBuilder.Sql("""
                -- Stable navigation codes for the controlled SUNAT catalog readers.
                DECLARE @Root int, @Identidad int, @Comprobantes int, @Ubicacion int, @Empresas int;

                INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                VALUES (N'catalogos-sunat', N'Catálogos SUNAT', N'CatalogosSunat', 30, 1, NULL, N'catalogos-sunat', N'layers');
                SET @Root = CONVERT(int, SCOPE_IDENTITY());

                INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                VALUES
                    (N'sunat-identidad', N'Identidad', NULL, 10, 1, @Root, NULL, N'users'),
                    (N'sunat-comprobantes', N'Comprobantes, pagos y tributos', NULL, 20, 1, @Root, NULL, N'grid'),
                    (N'sunat-ubicacion', N'Ubicación', NULL, 30, 1, @Root, NULL, N'folder'),
                    (N'sunat-empresas', N'Empresas y entidades', NULL, 40, 1, @Root, NULL, N'settings');

                SELECT @Identidad = idMenu FROM dbo.MenuSistema WHERE codigo = N'sunat-identidad';
                SELECT @Comprobantes = idMenu FROM dbo.MenuSistema WHERE codigo = N'sunat-comprobantes';
                SELECT @Ubicacion = idMenu FROM dbo.MenuSistema WHERE codigo = N'sunat-ubicacion';
                SELECT @Empresas = idMenu FROM dbo.MenuSistema WHERE codigo = N'sunat-empresas';

                INSERT dbo.MenuSistema (codigo, nombre, controllerNombre, orden, activo, idMenuPadre, destino, icono)
                VALUES
                    (N'sunat-documentos-identidad', N'Tipos de documento de identidad', N'CatalogosSunat', 10, 1, @Identidad, N'sunat-documentos-identidad', N'users'),
                    (N'sunat-tipo-contribuyente', N'Tipos de contribuyente', N'CatalogosSunat', 20, 1, @Identidad, N'sunat-tipo-contribuyente', N'shield'),
                    (N'sunat-tipos-comprobante', N'Tipos de comprobante', N'CatalogosSunat', 10, 1, @Comprobantes, N'sunat-tipos-comprobante', N'folder'),
                    (N'sunat-monedas', N'Monedas', N'CatalogosSunat', 20, 1, @Comprobantes, N'sunat-monedas', N'grid'),
                    (N'sunat-unidades-medida', N'Unidades de medida', N'CatalogosSunat', 30, 1, @Comprobantes, N'sunat-unidades-medida', N'layers'),
                    (N'sunat-medios-pago', N'Medios de pago', N'CatalogosSunat', 40, 1, @Comprobantes, N'sunat-medios-pago', N'bolt'),
                    (N'sunat-detracciones', N'Detracciones', N'CatalogosSunat', 50, 1, @Comprobantes, N'sunat-detracciones', N'sliders'),
                    (N'sunat-motivos-nc', N'Motivos de nota de crédito', N'CatalogosSunat', 60, 1, @Comprobantes, N'sunat-motivos-nc', N'folder'),
                    (N'sunat-motivos-nd', N'Motivos de nota de débito', N'CatalogosSunat', 70, 1, @Comprobantes, N'sunat-motivos-nd', N'folder'),
                    (N'sunat-motivos-traslado', N'Motivos de traslado', N'CatalogosSunat', 80, 1, @Comprobantes, N'sunat-motivos-traslado', N'bolt'),
                    (N'sunat-estados-envio', N'Estados de envío SUNAT', N'CatalogosSunat', 90, 1, @Comprobantes, N'sunat-estados-envio', N'spark'),
                    (N'sunat-ubigeos', N'Ubigeos', N'CatalogosSunat', 10, 1, @Ubicacion, N'sunat-ubigeos', N'grid'),
                    (N'sunat-tipos-via', N'Tipos de vía', N'CatalogosSunat', 20, 1, @Ubicacion, N'sunat-tipo-via', N'folder'),
                    (N'sunat-tipos-zona', N'Tipos de zona', N'CatalogosSunat', 30, 1, @Ubicacion, N'sunat-tipo-zona', N'folder'),
                    (N'sunat-tipos-actividad', N'Tipos de actividad económica', N'CatalogosSunat', 10, 1, @Empresas, N'sunat-tipo-actividad', N'layers'),
                    (N'sunat-tipos-entidad-financiera', N'Tipos de entidad financiera', N'CatalogosSunat', 20, 1, @Empresas, N'sunat-tipos-entidad-financiera', N'shield'),
                    (N'sunat-entidades-financieras', N'Entidades financieras', N'CatalogosSunat', 30, 1, @Empresas, N'sunat-entidades-financieras', N'users');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM dbo.PermisoRolMenu permission
                    INNER JOIN dbo.MenuSistema menu ON menu.idMenu = permission.idMenu
                    WHERE menu.codigo LIKE N'sunat-%' OR menu.codigo = N'catalogos-sunat')
                    THROW 51020, 'No se revierte el módulo SUNAT mientras existan permisos asignados a sus menús.', 1;

                IF EXISTS (
                    SELECT 1 FROM dbo.MenuSistema child
                    INNER JOIN dbo.MenuSistema parent ON parent.idMenu = child.idMenuPadre
                    WHERE (parent.codigo LIKE N'sunat-%' OR parent.codigo = N'catalogos-sunat')
                      AND child.codigo NOT LIKE N'sunat-%')
                    THROW 51021, 'Reubica primero los menús agregados bajo Catálogos SUNAT.', 1;

                DELETE FROM dbo.MenuSistema WHERE codigo LIKE N'sunat-%';
                DELETE FROM dbo.MenuSistema WHERE codigo = N'catalogos-sunat';
                """);

            migrationBuilder.DropTable(
                name: "mae_detraccion");

            migrationBuilder.DropTable(
                name: "mae_entidadfinanciera");

            migrationBuilder.DropTable(
                name: "mae_moneda");

            migrationBuilder.DropTable(
                name: "mae_motivonc");

            migrationBuilder.DropTable(
                name: "mae_motivond");

            migrationBuilder.DropTable(
                name: "mae_motivotraslado");

            migrationBuilder.DropTable(
                name: "mae_tipoactividad");

            migrationBuilder.DropTable(
                name: "mae_tipocontribuyente");

            migrationBuilder.DropTable(
                name: "mae_tipodocumentopago");

            migrationBuilder.DropTable(
                name: "mae_tipomediopago");

            migrationBuilder.DropTable(
                name: "mae_via_tipo");

            migrationBuilder.DropTable(
                name: "mae_zona_tipo");

            migrationBuilder.DropTable(
                name: "ven_estadosunat");

            migrationBuilder.DropTable(
                name: "mae_entidadfinanciera_tipo");
        }
    }
}
