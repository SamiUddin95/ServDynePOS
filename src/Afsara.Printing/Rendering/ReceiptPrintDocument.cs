using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;

namespace Afsara.Printing.Rendering;

public sealed class ReceiptPrintDocument : PrintDocument
{
    private const int PaperWidth = 315;
    private const float ContentWidth = 270f;
    private const float Left = 10f;
    private const float Top = 6f;
    private const int BottomFeed = 40;

    private readonly Receipt _receipt;

    public ReceiptPrintDocument(Receipt receipt, string documentName)
    {
        _receipt = receipt;
        DocumentName = documentName;
        OriginAtMargins = false;

        var height = (int)Math.Ceiling(MeasureHeight()) + (int)Top + BottomFeed;
        DefaultPageSettings.PaperSize = new PaperSize("Receipt 80mm", PaperWidth, height);
        DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
    }

    private float MeasureHeight()
    {
        using var bitmap = new Bitmap(1, 1);
        bitmap.SetResolution(100, 100);
        using var g = Graphics.FromImage(bitmap);
        g.PageUnit = GraphicsUnit.Pixel;
        return _receipt.Measure(g, ContentWidth);
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        base.OnPrintPage(e);
        var g = e.Graphics!;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        _receipt.Draw(g, Left, Top, ContentWidth);
        e.HasMorePages = false;
    }
}
