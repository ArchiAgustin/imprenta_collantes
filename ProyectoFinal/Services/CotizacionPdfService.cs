using System.Globalization;
using MigraDocCore.DocumentObjectModel;
using MigraDocCore.DocumentObjectModel.Tables;
using MigraDocCore.Rendering;
using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public sealed class CotizacionPdfService
{
    private static readonly CultureInfo Peru = CultureInfo.GetCultureInfo("es-PE");

    public byte[] Generar(Cotizacion cotizacion)
    {
        var document = new Document();
        document.Info.Title = $"Cotización {cotizacion.NumeroInterno}";
        document.Info.Subject = "Propuesta comercial de Imprenta Collantes";
        document.Info.Author = "Imprenta Collantes";

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(3.1);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2.0);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.7);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.7);
        section.PageSetup.HeaderDistance = Unit.FromCentimeter(1.0);
        section.PageSetup.FooterDistance = Unit.FromCentimeter(1.0);

        var header = section.Headers.Primary.AddParagraph("IMPRENTA COLLANTES");
        header.Format.Font.Name = "Arial";
        header.Format.Font.Size = 12;
        header.Format.Font.Bold = true;
        header.Format.Font.Color = Colors.DarkBlue;
        header.Format.Borders.Bottom.Width = 0.75;
        header.Format.Borders.Bottom.Color = Colors.LightGray;
        header.Format.SpaceAfter = Unit.FromPoint(5);

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Name = "Arial";
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = Colors.Gray;
        footer.AddText("Cotización comercial · No es comprobante de pago tributario");
        footer.AddTab();
        footer.AddText("Página ");
        footer.AddPageField();

        var title = section.AddParagraph("COTIZACIÓN");
        title.Format.Font.Name = "Arial";
        title.Format.Font.Size = 20;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = Unit.FromPoint(3);
        section.AddParagraph(cotizacion.NumeroInterno).Format.Font.Size = 12;

        var info = section.AddTable();
        info.Borders.Visible = false;
        info.AddColumn(Unit.FromCentimeter(8.6));
        info.AddColumn(Unit.FromCentimeter(8.6));
        var customerRow = info.AddRow();
        AddFact(customerRow.Cells[0], "Cliente", cotizacion.ClienteNombreEmision
            ?? cotizacion.IdClienteNavigation?.Nombre ?? "No registrado");
        AddFact(customerRow.Cells[1], "Documento", cotizacion.ClienteDocumentoEmision ?? "No registrado");
        var dateRow = info.AddRow();
        AddFact(dateRow.Cells[0], "Emisión", cotizacion.FechaCreacion?.ToString("dd/MM/yyyy") ?? "No registrada");
        AddFact(dateRow.Cells[1], "Vigencia", cotizacion.FechaVencimiento?.ToString("dd/MM/yyyy") ?? "Sin vencimiento");
        var stateRow = info.AddRow();
        AddFact(stateRow.Cells[0], "Estado", cotizacion.Estado);
        AddFact(stateRow.Cells[1], "Responsable", cotizacion.IdUsuarioNavigation?.Email ?? "No registrado");

        if (!string.IsNullOrWhiteSpace(cotizacion.DetalleCotizacion))
        {
            var descriptionTitle = section.AddParagraph("Descripción del trabajo");
            descriptionTitle.Format.Font.Bold = true;
            descriptionTitle.Format.SpaceBefore = Unit.FromPoint(10);
            section.AddParagraph(cotizacion.DetalleCotizacion).Format.SpaceAfter = Unit.FromPoint(10);
        }

        var table = section.AddTable();
        table.Borders.Color = Colors.LightGray;
        table.Borders.Width = 0.5;
        table.TopPadding = 4;
        table.BottomPadding = 4;
        table.AddColumn(Unit.FromCentimeter(7.6));
        var quantityColumn = table.AddColumn(Unit.FromCentimeter(3.4));
        quantityColumn.Format.Alignment = ParagraphAlignment.Right;
        var priceColumn = table.AddColumn(Unit.FromCentimeter(3.0));
        priceColumn.Format.Alignment = ParagraphAlignment.Right;
        var amountColumn = table.AddColumn(Unit.FromCentimeter(3.2));
        amountColumn.Format.Alignment = ParagraphAlignment.Right;

        var heading = table.AddRow();
        heading.HeadingFormat = true;
        heading.Shading.Color = Colors.LightGray;
        heading.Format.Font.Bold = true;
        heading.Cells[0].AddParagraph("Producto / servicio");
        heading.Cells[1].AddParagraph("Cantidad");
        heading.Cells[2].AddParagraph("Precio presentación");
        heading.Cells[3].AddParagraph("Importe");

        var subtotal = 0m;
        foreach (var line in cotizacion.DetalleCotizacions.OrderBy(item => item.IdDetalleCotizacion))
        {
            var cantidad = line.Cantidad ?? 0;
            var precio = line.PrecioUnitario ?? 0m;
            var importe = decimal.Round(cantidad * precio, 2, MidpointRounding.AwayFromZero);
            subtotal += importe;

            var row = table.AddRow();
            row.Cells[0].AddParagraph(line.ProductoNombreEmision
                ?? line.IdProductoNavigation?.Nombre ?? $"Producto #{line.IdProducto}");
            var quantity = row.Cells[1].AddParagraph($"{cantidad} × {line.PresentacionDescripcion ?? "Unidad base"}");
            if (line.FactorPresentacion != 1m && !string.IsNullOrWhiteSpace(line.UnidadBaseDescripcion))
            {
                var conversion = row.Cells[1].AddParagraph(
                    $"= {cantidad * line.FactorPresentacion:0.###} {line.UnidadBaseDescripcion}");
                conversion.Format.Font.Size = 8;
                conversion.Format.Font.Color = Colors.Gray;
            }
            row.Cells[2].AddParagraph(precio.ToString("C2", Peru));
            row.Cells[3].AddParagraph(importe.ToString("C2", Peru));
        }

        subtotal = decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        var igv = decimal.Round(subtotal * 0.18m, 2, MidpointRounding.AwayFromZero);
        var totals = section.AddTable();
        totals.Borders.Visible = false;
        totals.AddColumn(Unit.FromCentimeter(11.5));
        var totalValueColumn = totals.AddColumn(Unit.FromCentimeter(5.7));
        totalValueColumn.Format.Alignment = ParagraphAlignment.Right;
        AddTotal(totals, "Subtotal sin IGV", subtotal);
        AddTotal(totals, "IGV (18 %)", igv);
        var finalTotal = totals.AddRow();
        finalTotal.Format.Font.Bold = true;
        finalTotal.Cells[0].AddParagraph("TOTAL");
        finalTotal.Cells[1].AddParagraph((cotizacion.Total ?? subtotal + igv).ToString("C2", Peru));
        finalTotal.Cells[1].Format.Alignment = ParagraphAlignment.Right;

        var legal = section.AddParagraph("Documento comercial de propuesta. No es boleta, factura ni comprobante emitido a SUNAT.");
        legal.Format.Font.Size = 8;
        legal.Format.Font.Color = Colors.Gray;
        legal.Format.SpaceBefore = Unit.FromPoint(12);

        var renderer = new PdfDocumentRenderer(unicode: true) { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream);
        return stream.ToArray();
    }

    private static void AddFact(Cell cell, string label, string value)
    {
        var paragraph = cell.AddParagraph();
        paragraph.Format.SpaceAfter = Unit.FromPoint(5);
        paragraph.AddFormattedText(label + ": ", TextFormat.Bold);
        paragraph.AddText(value);
    }

    private static void AddTotal(Table table, string label, decimal value)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label);
        row.Cells[1].AddParagraph(value.ToString("C2", Peru));
        row.Cells[1].Format.Alignment = ParagraphAlignment.Right;
    }
}
