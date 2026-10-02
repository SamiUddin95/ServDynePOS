using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Dialogs;

public class DiscountDialog : Form
{
    private readonly RadioButton _percent;
    private readonly RadioButton _amount;
    private readonly NumericUpDown _value;

    public decimal DiscountPercent => _percent.Checked ? _value.Value : 0;
    public decimal DiscountAmount => _amount.Checked ? _value.Value : 0;

    public DiscountDialog(decimal currentPercent, decimal currentAmount, decimal subTotal)
    {
        Theme.ApplyDialog(this, "Apply Discount", new Size(420, 280));

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(layout);

        var header = Theme.Label($"Sub Total: {Ui.Money(subTotal)}", 11f, FontStyle.Bold);
        layout.Controls.Add(header, 0, 0);
        layout.SetColumnSpan(header, 2);

        _percent = new RadioButton { Text = "Percentage (%)", Font = Theme.Font(10.5f), AutoSize = true, Checked = currentAmount == 0 };
        _amount = new RadioButton { Text = "Flat Amount", Font = Theme.Font(10.5f), AutoSize = true, Checked = currentAmount > 0 };
        layout.Controls.Add(_percent, 0, 1);
        layout.Controls.Add(_amount, 1, 1);

        _value = Theme.Numeric(1_000_000, 2);
        _value.Font = Theme.Font(14f, FontStyle.Bold);
        _value.Value = currentAmount > 0 ? currentAmount : currentPercent;
        layout.Controls.Add(_value, 0, 2);
        layout.SetColumnSpan(_value, 2);

        var ok = Theme.Button("APPLY", Theme.Success);
        ok.Click += (_, _) =>
        {
            if (_percent.Checked && _value.Value > 100)
            {
                Ui.Warn(this, "Percentage cannot exceed 100%.");
                return;
            }
            if (_amount.Checked && _value.Value > subTotal)
            {
                Ui.Warn(this, "Discount cannot exceed the sub total.");
                return;
            }
            DialogResult = DialogResult.OK;
        };
        var remove = Theme.OutlineButton("Remove Discount");
        remove.Click += (_, _) =>
        {
            _value.Value = 0;
            _percent.Checked = true;
            DialogResult = DialogResult.OK;
        };
        layout.Controls.Add(remove, 0, 4);
        layout.Controls.Add(ok, 1, 4);

        AcceptButton = ok;
        Shown += (_, _) => { _value.Focus(); _value.Select(0, _value.Text.Length); };
    }
}
