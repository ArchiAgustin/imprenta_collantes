using System.Globalization;
using ClosedXML.Excel;
using MigraDocCore.DocumentObjectModel;
using MigraDocCore.DocumentObjectModel.Tables;
using MigraDocCore.Rendering;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class AnalyticsExportService(IAnalyticsService analytics)
{
    private static readonly CultureInfo Peru = CultureInfo.GetCultureInfo("es-PE");

    public async Task<byte[]> ExcelAsync(DateOnly desde, DateOnly hasta, int umbralStock)
    {
        var report = await analytics.ObtenerAsync(desde, hasta, umbralStock);
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Resumen", new[] { "Indicador", "Valor" }, new object?[][]
        {
            new object?[] { "Periodo (Perú)", $"{desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}" },
            new object?[] { "Ventas confirmadas con IGV", report.VentasConIgv },
            new object?[] { "Ventas confirmadas", report.VentasConfirmadas },
            new object?[] { "Ventas canceladas excluidas", report.VentasCanceladas },
            new object?[] { "Ventas sin IGV", report.VentasSinIgv },
            new object?[] { "IGV incluido", report.Igv },
            new object?[] { "Cotizaciones emitidas", report.CotizacionesEmitidas },
            new object?[] { "Cotizaciones convertidas", report.CotizacionesConvertidas },
            new object?[] { "Tasa de conversión (%)", report.TasaConversionPorcentaje },
            new object?[] { "Cotizaciones vigentes", report.CotizacionesVigentes },
            new object?[] { "Cotizaciones vencidas", report.CotizacionesVencidas },
            new object?[] { "Clientes con ventas", report.ClientesConVentas },
            new object?[] { "Clientes recurrentes (≥ 2 compras)", report.ClientesRecurrentes },
            new object?[] { "% clientes recurrentes", report.PorcentajeClientesRecurrentes },
            new object?[] { "Ticket medio recurrente", report.TicketMedioRecurrente },
            new object?[] { "Ticket medio nuevo", report.TicketMedioNuevo },
            new object?[] { "Utilidad estimada parcial (sin IGV)", report.UtilidadEstimada },
            new object?[] { "Líneas sin costo conocido", report.LineasSinCosto },
            new object?[] { "Umbral de alerta de stock", umbralStock },
            new object?[] { "Alcance", "Ventas comerciales internas; no son facturación tributaria." }
        });

        AddSheet(workbook, "Tendencia", new[] { "Periodo", "Ventas confirmadas", "Importe con IGV" },
            report.Tendencia.Select(item => new object?[] { item.Etiqueta, item.Ventas, item.TotalConIgv }));
        AddSheet(workbook, "Productos", new[] { "Producto / servicio", "Unidades base", "Ventas con IGV", "% acumulado" },
            report.Productos.Select(item => new object?[] { item.Nombre, item.Unidades, item.TotalConIgv, item.PorcentajeAcumulado }));
        AddSheet(workbook, "Clientes", new[] { "Cliente", "Ventas", "Total con IGV", "Ticket medio", "Grupo ABC", "Recurrente", "Última compra", "Días sin comprar", "Frecuencia media (días)" },
            report.Clientes.Select(item => new object?[]
            {
                item.Nombre, item.Ventas, item.TotalConIgv, item.TicketPromedio, item.Segmento,
                item.Recurrente ? "Sí" : "No",
                item.UltimaCompra?.ToString("dd/MM/yyyy"),
                item.DiasDesdeUltimaCompra,
                item.FrecuenciaMediaDias
            }));
        AddSheet(workbook, "Taller", new[] { "Estado actual", "Pedidos" }, new object?[][]
        {
            new object?[] { "En cola", report.EnCola },
            new object?[] { "En impresión / acabado", report.EnProduccion },
            new object?[] { "Listos para recojo", report.ListosParaRecojo },
            new object?[] { "Entregados", report.Entregados },
            new object?[] { "Cancelados", report.PedidosCancelados }
        });
        AddSheet(workbook, "Stock", new[] { "Artículo", "Existencias base", "Almacén" },
            report.AlertasStock.Select(item => new object?[] { item.Nombre, item.Existencias, item.Almacen ?? "Stock heredado" }));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> PdfAsync(DateOnly desde, DateOnly hasta, int umbralStock)
    {
        var report = await analytics.ObtenerAsync(desde, hasta, umbralStock);
        var document = new Document();
        document.Info.Title = "Reporte de analítica comercial";
        document.Info.Author = "Imprenta Collantes";
        document.Info.Subject = $"Periodo {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}";
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Landscape;
        section.PageSetup.TopMargin = Unit.FromCentimeter(2.4);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.6);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.6);
        section.Headers.Primary.AddParagraph("IMPRENTA COLLANTES · ANALÍTICA COMERCIAL").Format.Font.Bold = true;
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = Colors.Gray;
        footer.AddText("Fecha en horario peruano · cifras operativas, no facturación tributaria");
        footer.AddTab();
        footer.AddText("Página ");
        footer.AddPageField();

        var title = section.AddParagraph("Reporte de analítica");
        title.Format.Font.Size = 20;
        title.Format.Font.Bold = true;
        section.AddParagraph($"Periodo: {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy} · Umbral de stock: {umbralStock}");
        section.AddParagraph("Ventas confirmadas con IGV: " + report.VentasConIgv.ToString("C2", Peru)
            + $" ({report.VentasConfirmadas} confirmadas; {report.VentasCanceladas} canceladas excluidas)");
        section.AddParagraph($"Ventas sin IGV: {report.VentasSinIgv.ToString("C2", Peru)} · IGV: {report.Igv.ToString("C2", Peru)}");
        section.AddParagraph(report.UtilidadEstimada is { } profit
            ? $"Utilidad estimada sin IGV{(report.LineasSinCosto > 0 ? " (parcial)" : "")}: {profit.ToString("C2", Peru)}; {report.LineasSinCosto} de {report.TotalLineas} líneas sin costo."
            : $"Utilidad no calculable; faltan costos en {report.LineasSinCosto} de {report.TotalLineas} líneas.");

        AddPdfTable(section, "Cotizaciones", new[] { "Emitidas", "Convertidas", "Tasa (%)", "Vigentes", "Vencidas" }, new[]
        {
            new[] { report.CotizacionesEmitidas.ToString(), report.CotizacionesConvertidas.ToString(),
                report.TasaConversionPorcentaje.ToString("N1", Peru), report.CotizacionesVigentes.ToString(), report.CotizacionesVencidas.ToString() }
        });
        AddPdfTable(section, "Tendencia de ventas", new[] { "Periodo", "Ventas", "Total con IGV" },
            report.Tendencia.Select(item => new[] { item.Etiqueta, item.Ventas.ToString(), item.TotalConIgv.ToString("C2", Peru) }));
        AddPdfTable(section, "Productos y servicios · Top 10", new[] { "Producto / servicio", "Unidades base", "Ventas con IGV", "% acumulado" },
            report.Productos.Select(item => new[] { item.Nombre, item.Unidades.ToString("0.###", Peru), item.TotalConIgv.ToString("C2", Peru), item.PorcentajeAcumulado.ToString("N1", Peru) + " %" }));
        AddPdfTable(section, "Clientes · Top 10", new[] { "Cliente", "Ventas", "Total con IGV", "Ticket medio", "ABC", "Recurrente", "Días sin comprar" },
            report.Clientes.Select(item => new[]
            {
                item.Nombre, item.Ventas.ToString(), item.TotalConIgv.ToString("C2", Peru),
                item.TicketPromedio.ToString("C2", Peru), item.Segmento,
                item.Recurrente ? "Sí" : "No",
                item.DiasDesdeUltimaCompra?.ToString() ?? "—"
            }));
        AddPdfTable(section, "Estado actual del taller", new[] { "Estado", "Pedidos" }, new[]
        {
            new[] { "En cola", report.EnCola.ToString() },
            new[] { "En impresión / acabado", report.EnProduccion.ToString() },
            new[] { "Listos para recojo", report.ListosParaRecojo.ToString() },
            new[] { "Entregados", report.Entregados.ToString() },
            new[] { "Cancelados", report.PedidosCancelados.ToString() }
        });
        AddPdfTable(section, "Alertas de stock actual", new[] { "Artículo", "Existencias base", "Almacén" },
            report.AlertasStock.Select(item => new[] { item.Nombre, item.Existencias.ToString("0.###", Peru), item.Almacen ?? "Stock heredado" }));

        var renderer = new PdfDocumentRenderer(unicode: true) { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream);
        return stream.ToArray();
    }

    private static void AddSheet(XLWorkbook workbook, string name, IReadOnlyList<string> headers,
        IEnumerable<object?[]> rows)
    {
        var data = rows.ToList();
        var sheet = workbook.Worksheets.Add(name);
        sheet.Cell(1, 1).Value = "Imprenta Collantes · Reporte analítico";
        sheet.Range(1, 1, 1, headers.Count).Merge();
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        for (var col = 0; col < headers.Count; col++)
        {
            var header = sheet.Cell(3, col + 1);
            header.Value = headers[col];
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E9EFF0");
        }

        var rowIndex = 4;
        foreach (var row in data)
        {
            for (var col = 0; col < headers.Count; col++)
            {
                var cell = sheet.Cell(rowIndex, col + 1);
                switch (row[col])
                {
                    case null: break;
                    case decimal value: cell.Value = value; break;
                    case int value: cell.Value = value; break;
                    case double value: cell.Value = value; break;
                    case DateTime value: cell.Value = value; break;
                    default: cell.Value = Convert.ToString(row[col], CultureInfo.InvariantCulture) ?? string.Empty; break;
                }
            }
            rowIndex++;
        }
        if (data.Count > 0) sheet.Range(3, 1, rowIndex - 1, headers.Count).SetAutoFilter();
        for (var column = 0; column < headers.Count; column++)
        {
            var header = headers[column];
            var currencyColumn = header.Contains("con IGV", StringComparison.OrdinalIgnoreCase)
                || header.Contains("sin IGV", StringComparison.OrdinalIgnoreCase)
                || header.Contains("Ticket", StringComparison.OrdinalIgnoreCase)
                || header.Contains("Importe", StringComparison.OrdinalIgnoreCase);
            if (currencyColumn) sheet.Column(column + 1).Style.NumberFormat.Format = "\"S/ \"#,##0.00;[Red]-\"S/ \"#,##0.00";
            else if (header.Contains("%", StringComparison.Ordinal)) sheet.Column(column + 1).Style.NumberFormat.Format = "0.0\\%";
            else if (header.Contains("Unidades", StringComparison.OrdinalIgnoreCase)
                || header.Contains("Existencias", StringComparison.OrdinalIgnoreCase))
                sheet.Column(column + 1).Style.NumberFormat.Format = "#,##0.###";
        }
        if (name == "Resumen")
        {
            for (var row = 4; row < rowIndex; row++)
            {
                var label = sheet.Cell(row, 1).GetString();
                if (label.Contains("IGV", StringComparison.OrdinalIgnoreCase)
                    || label.Contains("Utilidad", StringComparison.OrdinalIgnoreCase))
                    sheet.Cell(row, 2).Style.NumberFormat.Format = "\"S/ \"#,##0.00;[Red]-\"S/ \"#,##0.00";
                else if (label.Contains("conversión", StringComparison.OrdinalIgnoreCase))
                    sheet.Cell(row, 2).Style.NumberFormat.Format = "0.0\\%";
            }
        }
        sheet.SheetView.FreezeRows(3);
        sheet.Columns().AdjustToContents(10, 50);
    }

    private static void AddPdfTable(Section section, string title, IReadOnlyList<string> headers,
        IEnumerable<string[]> rows)
    {
        var heading = section.AddParagraph(title);
        heading.Format.Font.Size = 12;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(10);
        heading.Format.SpaceAfter = Unit.FromPoint(4);

        var table = section.AddTable();
        table.Borders.Color = Colors.LightGray;
        table.Borders.Width = 0.5;
        table.TopPadding = 3;
        table.BottomPadding = 3;
        var width = 26.5 / headers.Count;
        for (var index = 0; index < headers.Count; index++) table.AddColumn(Unit.FromCentimeter(width));
        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Colors.LightGray;
        header.Format.Font.Bold = true;
        for (var index = 0; index < headers.Count; index++) header.Cells[index].AddParagraph(headers[index]);
        foreach (var values in rows)
        {
            var row = table.AddRow();
            for (var index = 0; index < headers.Count; index++) row.Cells[index].AddParagraph(index < values.Length ? values[index] : string.Empty);
        }
    }
}
