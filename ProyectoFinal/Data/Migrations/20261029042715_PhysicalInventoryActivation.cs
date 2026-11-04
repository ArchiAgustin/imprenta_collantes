using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class PhysicalInventoryActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "conteoRegistrado",
                table: "CorteInventarioDetalle",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fechaAprobacionUtc",
                table: "CorteInventario",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "conteoEnCurso",
                table: "Almacen",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fechaActivacionUtc",
                table: "Almacen",
                type: "datetimeoffset",
                nullable: true);

            // Create the agreed warehouse and base unit even in a fresh database;
            // preserve imported unit IDs where the reference catalog already exists.
            // Never copy legacy Producto.Stock into Stock: opening needs a physical count.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM dbo.Almacen WHERE nombre = N'Almacén principal')
    INSERT INTO dbo.Almacen (nombre, activo, conteoEnCurso) VALUES (N'Almacén principal', 1, 0);

IF NOT EXISTS (SELECT 1 FROM dbo.UnidadMedida WHERE descripcion = N'UNIDAD (BIENES)' AND activo = 1)
    INSERT INTO dbo.UnidadMedida (descripcion, descripcionCorta, codigoSunat, activo)
    VALUES (N'UNIDAD (BIENES)', N'Unidad', N'NIU', 1);

DECLARE @unidad int = (SELECT TOP (1) idUnidadMedida FROM dbo.UnidadMedida
                       WHERE descripcion = N'UNIDAD (BIENES)' AND activo = 1 ORDER BY idUnidadMedida);
IF @unidad IS NOT NULL
BEGIN
    UPDATE dbo.Producto SET idUnidadMedidaBase = @unidad
    WHERE esServicio = 0 AND idUnidadMedidaBase IS NULL;

    INSERT INTO dbo.PresentacionProducto (idProducto, idUnidadMedida, descripcion, factor, activo)
    SELECT p.idProducto, @unidad, N'Unidad', 1.000, 1 FROM dbo.Producto p
    WHERE p.esServicio = 0 AND p.idUnidadMedidaBase = @unidad
      AND NOT EXISTS (SELECT 1 FROM dbo.PresentacionProducto x
                      WHERE x.idProducto = p.idProducto AND x.idUnidadMedida = @unidad AND x.descripcion = N'Unidad');
END;");

            // Reject writes from a stale, still-running instance during/after cutover.
            migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER dbo.TR_Producto_BloquearStockLegado ON dbo.Producto AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(stock)
       AND EXISTS (SELECT 1 FROM dbo.Almacen WITH (UPDLOCK, HOLDLOCK)
                   WHERE nombre = N'Almacén principal' AND (conteoEnCurso = 1 OR fechaActivacionUtc IS NOT NULL))
       AND EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.idProducto = d.idProducto
                   WHERE ISNULL(i.stock, -1) <> ISNULL(d.stock, -1))
        THROW 51001, N'El stock legado no se puede modificar durante el conteo ni tras la activacion del inventario.', 1;
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.TR_Producto_BloquearStockLegado;");
            migrationBuilder.DropColumn(
                name: "conteoRegistrado",
                table: "CorteInventarioDetalle");

            migrationBuilder.DropColumn(
                name: "fechaAprobacionUtc",
                table: "CorteInventario");

            migrationBuilder.DropColumn(
                name: "conteoEnCurso",
                table: "Almacen");

            migrationBuilder.DropColumn(
                name: "fechaActivacionUtc",
                table: "Almacen");
        }
    }
}
