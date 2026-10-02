namespace Afsara.WinForms.Common;

public static class Theme
{
    public static readonly Color Header = Color.FromArgb(15, 23, 42);
    public static readonly Color Sidebar = Color.FromArgb(30, 41, 59);
    public static readonly Color Background = Color.FromArgb(241, 245, 249);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(226, 232, 240);
    public static readonly Color Text = Color.FromArgb(15, 23, 42);
    public static readonly Color Muted = Color.FromArgb(100, 116, 139);

    public static readonly Color Primary = Color.FromArgb(37, 99, 235);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);
    public static readonly Color Warning = Color.FromArgb(234, 88, 12);
    public static readonly Color Purple = Color.FromArgb(124, 58, 237);
    public static readonly Color Teal = Color.FromArgb(13, 148, 136);
    public static readonly Color Gray = Color.FromArgb(71, 85, 105);

    public static readonly Color[] TilePalette =
    {
        Color.FromArgb(37, 99, 235), Color.FromArgb(22, 163, 74), Color.FromArgb(234, 88, 12),
        Color.FromArgb(124, 58, 237), Color.FromArgb(13, 148, 136), Color.FromArgb(219, 39, 119),
        Color.FromArgb(202, 138, 4), Color.FromArgb(79, 70, 229)
    };

    public const string FontName = "Segoe UI";
    public static Font Font(float size = 10f, FontStyle style = FontStyle.Regular) => new(FontName, size, style);

    public static Button Button(string text, Color back, float fontSize = 10f, Color? fore = null)
    {
        var button = new Button
        {
            Text = text,
            BackColor = back,
            ForeColor = fore ?? Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = Font(fontSize, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            Margin = new Padding(3),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Darken(back, 0.12f);
        button.FlatAppearance.MouseDownBackColor = Darken(back, 0.22f);
        return button;
    }

    public static Button OutlineButton(string text, float fontSize = 10f)
    {
        var button = Button(text, Surface, fontSize, Text);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = Background;
        return button;
    }

    public static Label Label(string text, float size = 10f, FontStyle style = FontStyle.Regular, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Font(size, style),
        ForeColor = color ?? Text,
        Margin = new Padding(3, 6, 3, 3)
    };

    public static TextBox TextBox(string placeholder = "") => new()
    {
        PlaceholderText = placeholder,
        Font = Font(11f),
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
        Margin = new Padding(3, 4, 3, 4)
    };

    public static NumericUpDown Numeric(decimal max = 1_000_000, int decimals = 0) => new()
    {
        Maximum = max,
        Minimum = 0,
        DecimalPlaces = decimals,
        ThousandsSeparator = true,
        Font = Font(11f),
        Dock = DockStyle.Fill,
        Margin = new Padding(3, 4, 3, 4)
    };

    public static ComboBox Combo() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Font = Font(11f),
        Dock = DockStyle.Fill,
        Margin = new Padding(3, 4, 3, 4)
    };

    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Sidebar;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = Font(10f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Sidebar;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.DefaultCellStyle.Font = Font(10.5f);
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        grid.RowTemplate.Height = 36;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.Dock = DockStyle.Fill;
    }

    public static Color Darken(Color color, float amount) => Color.FromArgb(
        color.A,
        (int)(color.R * (1 - amount)),
        (int)(color.G * (1 - amount)),
        (int)(color.B * (1 - amount)));

    public static void ApplyDialog(Form form, string title, Size size)
    {
        form.Text = title;
        Brand.Apply(form);
        form.Font = Font(10f);
        form.BackColor = Background;
        form.StartPosition = FormStartPosition.CenterParent;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowInTaskbar = false;
        form.ClientSize = size;
        form.KeyPreview = true;
    }
}
