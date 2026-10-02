using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Dialogs;

public class InputDialog : Form
{
    private readonly TextBox _input;

    private InputDialog(string title, string prompt, string initial, bool password, bool multiline)
    {
        Theme.ApplyDialog(this, title, new Size(440, multiline ? 250 : 190));

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(layout);

        var label = Theme.Label(prompt, 10.5f, FontStyle.Bold);
        layout.Controls.Add(label, 0, 0);
        layout.SetColumnSpan(label, 2);

        _input = Theme.TextBox();
        _input.Text = initial;
        _input.UseSystemPasswordChar = password;
        _input.Multiline = multiline;
        if (multiline) _input.ScrollBars = ScrollBars.Vertical;
        layout.Controls.Add(_input, 0, 1);
        layout.SetColumnSpan(_input, 2);

        var ok = Theme.Button("OK", Theme.Primary);
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.OutlineButton("Cancel");
        cancel.DialogResult = DialogResult.Cancel;
        layout.Controls.Add(cancel, 0, 2);
        layout.Controls.Add(ok, 1, 2);

        if (!multiline) AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) => { _input.Focus(); _input.SelectAll(); };
    }

    public static string? Prompt(IWin32Window owner, string title, string prompt, string initial = "", bool password = false, bool multiline = false)
    {
        using var dialog = new InputDialog(title, prompt, initial, password, multiline);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._input.Text : null;
    }
}
