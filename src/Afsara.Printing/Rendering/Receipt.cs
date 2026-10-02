using System.Drawing;
using System.Drawing.Drawing2D;

namespace Afsara.Printing.Rendering;

public enum RuleStyle
{
    Dashed,
    Solid,
    Double
}

public readonly record struct Cell(string Text, float Width, StringAlignment Align = StringAlignment.Near);

public sealed class Receipt
{
    public const string FontFamily = "Arial";
    private readonly List<ReceiptElement> _elements = new();

    public Receipt Text(string text, float size = 8.5f, FontStyle style = FontStyle.Regular, StringAlignment align = StringAlignment.Near)
    {
        if (!string.IsNullOrWhiteSpace(text)) _elements.Add(new TextElement(text, size, style, align));
        return this;
    }

    public Receipt Center(string text, float size = 8.5f, FontStyle style = FontStyle.Regular) =>
        Text(text, size, style, StringAlignment.Center);

    public Receipt Band(string text, float size = 10f)
    {
        _elements.Add(new BandElement(text, size));
        return this;
    }

    public Receipt Rule(RuleStyle style = RuleStyle.Dashed)
    {
        _elements.Add(new RuleElement(style));
        return this;
    }

    public Receipt Row(float size, FontStyle style, params Cell[] cells)
    {
        _elements.Add(new RowElement(cells, size, style));
        return this;
    }

    public Receipt Row(params Cell[] cells) => Row(8.5f, FontStyle.Regular, cells);

    public Receipt Pair(string left, string right, float size = 8.5f, FontStyle style = FontStyle.Regular) =>
        Row(size, style, new Cell(left, 0.5f), new Cell(right, 0.5f, StringAlignment.Far));

    public Receipt Space(float height = 6f)
    {
        _elements.Add(new SpaceElement(height));
        return this;
    }

    public Receipt Image(string? path, float maxHeight = 70f)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return this;
        try
        {
            using var stream = File.OpenRead(path);
            _elements.Add(new ImageElement(System.Drawing.Image.FromStream(stream), maxHeight));
        }
        catch
        {
        }
        return this;
    }

    public float Measure(Graphics g, float width) => _elements.Sum(e => e.Measure(g, width));

    public void Draw(Graphics g, float x, float y, float width)
    {
        foreach (var element in _elements)
        {
            element.Draw(g, x, y, width);
            y += element.Measure(g, width);
        }
    }

    internal static StringFormat Format(StringAlignment align) => new()
    {
        Alignment = align,
        LineAlignment = StringAlignment.Near,
        Trimming = StringTrimming.Word
    };
}

internal abstract class ReceiptElement
{
    public abstract float Measure(Graphics g, float width);
    public abstract void Draw(Graphics g, float x, float y, float width);
}

internal sealed class TextElement(string text, float size, FontStyle style, StringAlignment align) : ReceiptElement
{
    public override float Measure(Graphics g, float width)
    {
        using var font = new Font(Receipt.FontFamily, size, style);
        using var format = Receipt.Format(align);
        return g.MeasureString(text, font, (int)width, format).Height + 1;
    }

    public override void Draw(Graphics g, float x, float y, float width)
    {
        using var font = new Font(Receipt.FontFamily, size, style);
        using var format = Receipt.Format(align);
        var height = g.MeasureString(text, font, (int)width, format).Height;
        g.DrawString(text, font, Brushes.Black, new RectangleF(x, y, width, height + 1), format);
    }
}

internal sealed class BandElement(string text, float size) : ReceiptElement
{
    private const float Padding = 4f;

    public override float Measure(Graphics g, float width)
    {
        using var font = new Font(Receipt.FontFamily, size, FontStyle.Bold);
        return g.MeasureString(text, font, (int)width).Height + Padding * 2 + 4;
    }

    public override void Draw(Graphics g, float x, float y, float width)
    {
        using var font = new Font(Receipt.FontFamily, size, FontStyle.Bold);
        using var format = Receipt.Format(StringAlignment.Center);
        var textHeight = g.MeasureString(text, font, (int)width).Height;
        var rect = new RectangleF(x, y + 2, width, textHeight + Padding * 2);
        g.FillRectangle(Brushes.Black, rect);
        g.DrawString(text, font, Brushes.White, new RectangleF(rect.X, rect.Y + Padding, rect.Width, textHeight), format);
    }
}

internal sealed class RuleElement(RuleStyle style) : ReceiptElement
{
    public override float Measure(Graphics g, float width) => style == RuleStyle.Double ? 9f : 7f;

    public override void Draw(Graphics g, float x, float y, float width)
    {
        using var pen = new Pen(Color.Black, style == RuleStyle.Solid ? 1.2f : 1f);
        if (style == RuleStyle.Dashed) pen.DashStyle = DashStyle.Dash;

        var mid = y + 3.5f;
        if (style == RuleStyle.Double)
        {
            g.DrawLine(pen, x, mid - 1, x + width, mid - 1);
            g.DrawLine(pen, x, mid + 2, x + width, mid + 2);
        }
        else
        {
            g.DrawLine(pen, x, mid, x + width, mid);
        }
    }
}

internal sealed class RowElement(Cell[] cells, float size, FontStyle style) : ReceiptElement
{
    public override float Measure(Graphics g, float width)
    {
        using var font = new Font(Receipt.FontFamily, size, style);
        return cells.Max(c =>
        {
            using var format = Receipt.Format(c.Align);
            return g.MeasureString(c.Text, font, (int)(width * c.Width), format).Height;
        }) + 1;
    }

    public override void Draw(Graphics g, float x, float y, float width)
    {
        using var font = new Font(Receipt.FontFamily, size, style);
        var height = Measure(g, width);
        var cx = x;
        foreach (var cell in cells)
        {
            var cellWidth = width * cell.Width;
            using var format = Receipt.Format(cell.Align);
            g.DrawString(cell.Text, font, Brushes.Black, new RectangleF(cx, y, cellWidth, height), format);
            cx += cellWidth;
        }
    }
}

internal sealed class SpaceElement(float height) : ReceiptElement
{
    public override float Measure(Graphics g, float width) => height;

    public override void Draw(Graphics g, float x, float y, float width)
    {
    }
}

internal sealed class ImageElement(Image image, float maxHeight) : ReceiptElement
{
    private SizeF Size(float width)
    {
        var ratio = Math.Min(width / image.Width, maxHeight / image.Height);
        return new SizeF(image.Width * ratio, image.Height * ratio);
    }

    public override float Measure(Graphics g, float width) => Size(width).Height + 4;

    public override void Draw(Graphics g, float x, float y, float width)
    {
        var size = Size(width);
        g.DrawImage(image, x + (width - size.Width) / 2, y, size.Width, size.Height);
    }
}
