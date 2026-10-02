namespace Afsara.WinForms.Common;

public static class Brand
{
    public const string Company = "ServDyne";
    public const string Product = "ServDyne POS";
    public const string Tagline = "Smart Dining. Seamless Operations.";

    public static readonly Color Navy = Color.FromArgb(22, 36, 71);
    public static readonly Color Orange = Color.FromArgb(247, 148, 29);

    private static Icon? _icon;
    private static Image? _logo;

    public static Icon? AppIcon => _icon ??= LoadIcon();

    public static Image? Logo => _logo ??= LoadLogo();

    public static void Apply(Form form)
    {
        if (AppIcon is { } icon) form.Icon = icon;
    }

    private static Icon? LoadIcon()
    {
        try
        {
            return Environment.ProcessPath is { } path ? Icon.ExtractAssociatedIcon(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static Image? LoadLogo()
    {
        using var stream = typeof(Brand).Assembly.GetManifestResourceStream("ServDyne.Logo.jpg");
        return stream is null ? null : Image.FromStream(new MemoryStream(ReadAll(stream)));
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
