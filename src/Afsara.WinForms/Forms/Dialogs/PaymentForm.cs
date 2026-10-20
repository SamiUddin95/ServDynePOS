using Afsara.Application.Orders;
using Afsara.Domain.Enums;
using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Dialogs;

public class PaymentForm : Form
{
    private readonly Dictionary<PaymentMethod, OrderTotals> _totals;
    private readonly string _currency;
    private readonly Dictionary<PaymentMethod, Button> _methodButtons = new();
    private readonly Label _subTotal = Value();
    private readonly Label _discount = Value();
    private readonly Label _service = Value();
    private readonly Label _taxLabel = Caption("Sales Tax");
    private readonly Label _tax = Value();
    private readonly Label _delivery = Value();
    private readonly Label _grandTotal;
    private readonly NumericUpDown _received;
    private readonly Label _change;
    private readonly FlowLayoutPanel _quickCash;

    public PaymentMethod Method { get; private set; } = PaymentMethod.Cash;
    public decimal Received => _received.Value;
    public bool PrintReceipt { get; private set; } = true;

    public PaymentForm(Dictionary<PaymentMethod, OrderTotals> totals, string currency, string orderTitle)
    {
        _totals = totals;
        _currency = currency;
        Theme.ApplyDialog(this, $"Settle Payment - {orderTitle}", new Size(760, 520));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var methods = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        for (var i = 0; i < 3; i++) methods.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        methods.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AddMethod(methods, PaymentMethod.Cash, "CASH", 0);
        AddMethod(methods, PaymentMethod.Card, "CARD", 1);
        AddMethod(methods, PaymentMethod.Online, "ONLINE / WALLET", 2);
        root.Controls.Add(methods, 0, 0);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Surface, Padding = new Padding(16) };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        root.Controls.Add(right, 1, 0);

        var row = 0;
        void AddLine(Control caption, Control value, float height = 28)
        {
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            right.Controls.Add(caption, 0, row);
            right.Controls.Add(value, 1, row);
            row++;
        }

        AddLine(Caption("Sub Total"), _subTotal);
        AddLine(Caption("Discount"), _discount);
        AddLine(Caption("Service Charges"), _service);
        AddLine(_taxLabel, _tax);
        AddLine(Caption("Delivery Charges"), _delivery);

        _grandTotal = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = Theme.Font(20f, FontStyle.Bold), ForeColor = Theme.Success };
        AddLine(new Label { Text = "GRAND TOTAL", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Theme.Font(14f, FontStyle.Bold) }, _grandTotal, 56);

        _received = Theme.Numeric(10_000_000);
        _received.Font = Theme.Font(18f, FontStyle.Bold);
        _received.TextAlign = HorizontalAlignment.Right;
        _received.ValueChanged += (_, _) => UpdateChange();
        _received.KeyUp += (_, _) => UpdateChange();
        AddLine(new Label { Text = "Amount Received", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Theme.Font(12f, FontStyle.Bold) }, _received, 50);

        _change = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = Theme.Font(16f, FontStyle.Bold), ForeColor = Theme.Primary };
        AddLine(new Label { Text = "Change", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Theme.Font(12f, FontStyle.Bold) }, _change, 44);

        _quickCash = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        right.Controls.Add(_quickCash, 0, row);
        right.SetColumnSpan(_quickCash, 2);
        row++;

        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        row++;

        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        var cancel = Theme.OutlineButton("Cancel (Esc)");
        cancel.DialogResult = DialogResult.Cancel;
        var complete = Theme.Button("COMPLETE ORDER (F2)\nNo Print", Theme.Primary, 10.5f);
        complete.Click += (_, _) => Settle(print: false);
        var settle = Theme.Button("SETTLE && PRINT (Enter)", Theme.Success, 12f);
        settle.Click += (_, _) => Settle(print: true);
        actions.Controls.Add(cancel, 0, 0);
        actions.Controls.Add(complete, 1, 0);
        actions.Controls.Add(settle, 2, 0);
        right.Controls.Add(actions, 0, row);
        right.SetColumnSpan(actions, 2);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.F2) return;
            e.Handled = true;
            Settle(print: false);
        };

        AcceptButton = settle;
        CancelButton = cancel;
        SelectMethod(PaymentMethod.Cash);
        Shown += (_, _) => { _received.Focus(); _received.Select(0, _received.Text.Length); };
    }

    private OrderTotals Current => _totals[Method];

    private void AddMethod(TableLayoutPanel panel, PaymentMethod method, string text, int row)
    {
        var button = Theme.Button(text, Theme.Gray, 13f);
        button.Margin = new Padding(0, 0, 0, 10);
        button.Click += (_, _) => SelectMethod(method);
        panel.Controls.Add(button, 0, row);
        _methodButtons[method] = button;
    }

    private void SelectMethod(PaymentMethod method)
    {
        Method = method;
        foreach (var (key, button) in _methodButtons)
        {
            var color = key == method ? Theme.Primary : Theme.Gray;
            button.BackColor = color;
            button.FlatAppearance.MouseOverBackColor = Theme.Darken(color, 0.12f);
        }

        var t = Current;
        _subTotal.Text = Ui.Money(t.SubTotal);
        _discount.Text = t.DiscountAmount > 0 ? $"- {Ui.Money(t.DiscountAmount)}" : "0";
        _service.Text = Ui.Money(t.ServiceCharge);
        _taxLabel.Text = $"Sales Tax ({t.TaxPercent:0.##}%)";
        _tax.Text = Ui.Money(t.TaxAmount);
        _delivery.Text = Ui.Money(t.DeliveryCharge);
        _grandTotal.Text = $"{_currency} {Ui.Money(t.GrandTotal)}";

        _received.Value = t.GrandTotal;
        _received.Enabled = method == PaymentMethod.Cash;
        BuildQuickCash(t.GrandTotal, method == PaymentMethod.Cash);
        UpdateChange();
    }

    private void BuildQuickCash(decimal total, bool enabled)
    {
        _quickCash.Controls.Clear();
        var amounts = new List<decimal> { total };
        foreach (var note in new decimal[] { 500, 1000, 2000, 5000 })
        {
            var rounded = Math.Ceiling(total / note) * note;
            if (rounded > total && !amounts.Contains(rounded)) amounts.Add(rounded);
        }

        foreach (var amount in amounts.Take(5))
        {
            var button = Theme.OutlineButton(amount == total ? "Exact" : Ui.Money(amount), 10.5f);
            button.Dock = DockStyle.None;
            button.Size = new Size(84, 42);
            button.Enabled = enabled;
            button.Click += (_, _) => _received.Value = Math.Min(amount, _received.Maximum);
            _quickCash.Controls.Add(button);
        }
    }

    private void UpdateChange()
    {
        var change = _received.Value - Current.GrandTotal;
        _change.Text = change >= 0 ? $"{_currency} {Ui.Money(change)}" : $"Short {Ui.Money(-change)}";
        _change.ForeColor = change >= 0 ? Theme.Primary : Theme.Danger;
    }

    private void Settle(bool print)
    {
        if (Method == PaymentMethod.Cash && _received.Value < Current.GrandTotal)
        {
            Ui.Warn(this, "Received amount is less than the grand total.");
            return;
        }
        PrintReceipt = print;
        DialogResult = DialogResult.OK;
    }

    private static Label Caption(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = Theme.Font(10.5f),
        ForeColor = Theme.Muted
    };

    private static Label Value() => new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight,
        Font = Theme.Font(11f, FontStyle.Bold)
    };
}
