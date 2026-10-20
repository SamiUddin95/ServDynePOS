using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Application.Days;
using Afsara.Application.Settings;
using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Dialogs;

public class CloseDayForm : Form
{
    private static readonly decimal[] Denominations = { 5000, 1000, 500, 100, 50, 20, 10, 5, 2, 1 };

    private readonly IBusinessDayService _days;
    private readonly ISettingsService _settings;
    private readonly IReceiptPrinter _printer;
    private readonly bool _canClose;

    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _warning = new();
    private readonly TableLayoutPanel _summary = new();
    private readonly DataGridView _count = new();
    private readonly Label _opening = Value();
    private readonly Label _cashSales = Value();
    private readonly Label _expected = Value();
    private readonly Label _counted = Value(16f);
    private readonly Label _varianceCaption = Caption("Difference", 12f, FontStyle.Bold);
    private readonly Label _variance = Value(16f);
    private readonly TextBox _notes = Theme.TextBox();
    private readonly Button _close;
    private DayReport? _report;
    private string _currency = string.Empty;

    public DayReport? ClosedReport { get; private set; }

    public CloseDayForm(IBusinessDayService days, ISettingsService settings, IReceiptPrinter printer, IUserSession session)
    {
        _days = days;
        _settings = settings;
        _printer = printer;
        _canClose = session.RequireUser().CanManage;

        Theme.ApplyDialog(this, "Day Close", new Size(1080, 780));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Header, Margin = Padding.Empty, Padding = new Padding(20, 10, 20, 8) };
        _subtitle.Dock = DockStyle.Top;
        _subtitle.Height = 28;
        _subtitle.ForeColor = Color.FromArgb(148, 163, 184);
        _subtitle.Font = Theme.Font(10.5f);
        header.Controls.Add(_subtitle);
        _title.Dock = DockStyle.Top;
        _title.Height = 40;
        _title.ForeColor = Color.White;
        _title.Font = Theme.Font(17f, FontStyle.Bold);
        _title.Text = "DAY CLOSE";
        header.Controls.Add(_title);
        root.Controls.Add(header, 0, 0);

        _warning.Dock = DockStyle.Fill;
        _warning.Visible = false;
        _warning.BackColor = Color.FromArgb(254, 226, 226);
        _warning.ForeColor = Theme.Danger;
        _warning.Font = Theme.Font(10.5f, FontStyle.Bold);
        _warning.Padding = new Padding(20, 10, 20, 10);
        _warning.Margin = Padding.Empty;
        root.Controls.Add(_warning, 0, 1);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        body.Controls.Add(BuildSummaryPanel(), 0, 0);
        body.Controls.Add(BuildCashPanel(out _close), 1, 0);
        root.Controls.Add(body, 0, 2);

        Load += async (_, _) => await this.RunAsync(LoadAsync);
    }

    private Control BuildSummaryPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, BackColor = Theme.Surface, Padding = new Padding(16, 10, 16, 12), Margin = new Padding(0, 0, 6, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        panel.Controls.Add(Theme.Label("SALES SUMMARY", 11.5f, FontStyle.Bold, Theme.Primary), 0, 0);

        _summary.Dock = DockStyle.Fill;
        _summary.ColumnCount = 2;
        _summary.AutoScroll = true;
        _summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        _summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        panel.Controls.Add(_summary, 0, 1);

        var printX = Theme.Button("Print X-Report (status)", Theme.Purple, 10.5f);
        printX.Click += async (_, _) => await PrintAsync(_report);
        panel.Controls.Add(printX, 0, 2);
        return panel;
    }

    private Control BuildCashPanel(out Button closeButton)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Surface, Padding = new Padding(16, 10, 16, 12), Margin = new Padding(6, 0, 0, 0) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        var row = 0;
        void Add(Control left, Control? right, float height)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            panel.Controls.Add(left, 0, row);
            if (right is null) panel.SetColumnSpan(left, 2);
            else panel.Controls.Add(right, 1, row);
            row++;
        }

        Add(Theme.Label("CASH COUNT", 11.5f, FontStyle.Bold, Theme.Primary),
            Caption("Type the number of notes in the yellow Count boxes", 9f, FontStyle.Italic), 34);

        Theme.StyleGrid(_count);
        _count.ReadOnly = false;
        _count.RowTemplate.Height = 28;
        _count.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _count.EditMode = DataGridViewEditMode.EditOnEnter;
        _count.Columns.Add(new DataGridViewTextBoxColumn { Name = "Note", HeaderText = "Denomination", ReadOnly = true, FillWeight = 40, DefaultCellStyle = { Font = Theme.Font(10.5f, FontStyle.Bold) } });
        _count.Columns.Add(new DataGridViewTextBoxColumn { Name = "Count", HeaderText = "Count (notes)", FillWeight = 25, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = Theme.Font(11f, FontStyle.Bold) } });
        _count.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Amount", ReadOnly = true, FillWeight = 35, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
        foreach (DataGridViewColumn column in _count.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        foreach (var note in Denominations)
        {
            var index = _count.Rows.Add(Ui.Money(note), string.Empty, string.Empty);
            _count.Rows[index].Cells["Count"].Style.BackColor = Color.FromArgb(255, 251, 235);
        }
        _count.CellEndEdit += (_, _) => UpdateCash();
        _count.CellEnter += (_, e) =>
        {
            if (e.ColumnIndex == _count.Columns["Count"]!.Index) return;
            var row = e.RowIndex;
            BeginInvoke(() => { if (row >= 0 && row < _count.Rows.Count) _count.CurrentCell = _count.Rows[row].Cells["Count"]; });
        };
        _count.EditingControlShowing += (_, e) =>
        {
            if (e.Control is not TextBox box) return;
            box.KeyPress -= DigitsOnly;
            box.KeyPress += DigitsOnly;
            box.TextChanged -= CountTyping;
            box.TextChanged += CountTyping;
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(_count, 0, row);
        panel.SetColumnSpan(_count, 2);
        row++;

        Add(Caption("Opening Balance"), _opening, 26);
        Add(Caption("+ Cash Sales"), _cashSales, 26);
        Add(Caption("Expected Cash in Drawer", 10.5f, FontStyle.Bold), _expected, 28);
        Add(Caption("Counted Cash", 12f, FontStyle.Bold), _counted, 36);
        Add(_varianceCaption, _variance, 36);

        Add(Caption("Closing Notes (required if cash is short or over)", 9.5f, FontStyle.Bold), null, 24);
        _notes.Multiline = true;
        _notes.ScrollBars = ScrollBars.Vertical;
        Add(_notes, null, 52);

        var cancel = Theme.OutlineButton("Cancel (Esc)", 10.5f);
        cancel.DialogResult = DialogResult.Cancel;
        CancelButton = cancel;
        closeButton = Theme.Button("CLOSE DAY && PRINT", Theme.Danger, 12f);
        closeButton.Click += async (_, _) => await CloseDayAsync();
        Add(cancel, closeButton, 56);
        return panel;
    }

    private void CountTyping(object? sender, EventArgs e)
    {
        if (_count.IsCurrentCellInEditMode) UpdateCash(normalize: false);
    }

    private static void DigitsOnly(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
    }

    private async Task LoadAsync()
    {
        _currency = (await _settings.GetAsync()).CurrencySymbol;
        _report = await _days.GetCurrentReportAsync();
        var r = _report;

        _title.Text = $"DAY CLOSE  -  {r.BusinessDate:dddd, dd MMM yyyy}";
        _subtitle.Text = $"Opened by {r.OpenedBy} at {r.OpenedAt:dd-MMM hh:mm tt}   |   Open for {Duration(DateTime.Now - r.OpenedAt)}";

        var blockers = new List<string>();
        if (r.RunningOrders > 0)
            blockers.Add($"{r.RunningOrders} running order(s) totalling {_currency} {Ui.Money(r.RunningAmount)} must be settled or cancelled before closing the day.");
        if (!_canClose)
            blockers.Add("Only a Manager or Admin can close the business day. You can still print the X-Report.");
        _warning.Text = string.Join(Environment.NewLine, blockers);
        _warning.Height = 20 + 24 * blockers.Count;
        _warning.Visible = blockers.Count > 0;
        _close.Enabled = blockers.Count == 0;
        _close.BackColor = _close.Enabled ? Theme.Danger : Theme.Border;

        RenderSummary(r);
        _opening.Text = Ui.Money(r.OpeningBalance);
        _cashSales.Text = Ui.Money(r.CashSales);
        _expected.Text = $"{_currency} {Ui.Money(r.ExpectedCash)}";
        UpdateCash();

        if (_close.Enabled)
        {
            _count.CurrentCell = _count.Rows[0].Cells["Count"];
            _count.Focus();
        }
    }

    private void RenderSummary(DayReport r)
    {
        _summary.SuspendLayout();
        _summary.Controls.Clear();
        _summary.RowStyles.Clear();
        var row = 0;
        void Line(string caption, string value, bool strong = false, Color? color = null)
        {
            _summary.RowStyles.Add(new RowStyle(SizeType.Absolute, strong ? 40 : 25));
            _summary.Controls.Add(Caption(caption, strong ? 13f : 10.5f, strong ? FontStyle.Bold : FontStyle.Regular, strong ? Theme.Text : null), 0, row);
            var label = Value(strong ? 16f : 11f);
            label.Text = value;
            if (color is { } c) label.ForeColor = c;
            _summary.Controls.Add(label, 1, row);
            row++;
        }
        void Section(string title)
        {
            _summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            var label = Caption(title, 9.5f, FontStyle.Bold, Theme.Muted);
            label.TextAlign = ContentAlignment.BottomLeft;
            _summary.Controls.Add(label, 0, row);
            _summary.SetColumnSpan(label, 2);
            row++;
        }

        Line("Completed Orders", r.CompletedOrders.ToString());
        Line("Cancelled Orders", r.CancelledOrders.ToString(), color: r.CancelledOrders > 0 ? Theme.Danger : null);
        Line("Gross Sales", Ui.Money(r.GrossSales));
        Line("Discounts", r.Discounts > 0 ? $"- {Ui.Money(r.Discounts)}" : "0", color: r.Discounts > 0 ? Theme.Danger : null);
        Line("Service Charges", Ui.Money(r.ServiceCharges));
        Line("Sales Tax", Ui.Money(r.Taxes));
        Line("Delivery Charges", Ui.Money(r.DeliveryCharges));
        Line("NET SALES", $"{_currency} {Ui.Money(r.NetSales)}", strong: true, color: Theme.Success);
        Section("PAYMENT BREAKDOWN");
        Line("Cash", Ui.Money(r.CashSales));
        Line("Card", Ui.Money(r.CardSales));
        Line("Online", Ui.Money(r.OnlineSales));
        Section("ORDER TYPE BREAKDOWN");
        Line("Dine In", Ui.Money(r.DineInSales));
        Line("Take Away", Ui.Money(r.TakeAwaySales));
        Line("Delivery", Ui.Money(r.DeliverySales));
        _summary.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _summary.RowCount = row + 1;
        _summary.ResumeLayout();
    }

    private List<CashCountLine> CountLines()
    {
        var lines = new List<CashCountLine>();
        for (var i = 0; i < Denominations.Length; i++)
        {
            var count = int.TryParse(CountText(i), out var value) && value > 0 ? value : 0;
            lines.Add(new CashCountLine(Denominations[i], count));
        }
        return lines;
    }

    private string? CountText(int row)
    {
        var cell = _count.Rows[row].Cells["Count"];
        var editing = _count.IsCurrentCellInEditMode && _count.CurrentCell == cell && _count.EditingControl is not null;
        return (editing ? _count.EditingControl!.Text : Convert.ToString(cell.Value))?.Trim();
    }

    private bool IsEntered(int row) => int.TryParse(CountText(row), out var value) && value >= 0;

    private bool HasCount() => Enumerable.Range(0, Denominations.Length).Any(IsEntered);

    private void UpdateCash(bool normalize = true)
    {
        var lines = CountLines();
        for (var i = 0; i < lines.Count; i++)
        {
            var entered = IsEntered(i);
            if (normalize) _count.Rows[i].Cells["Count"].Value = entered ? lines[i].Count.ToString() : string.Empty;
            _count.Rows[i].Cells["Amount"].Value = entered ? Ui.Money(lines[i].Amount) : string.Empty;
        }

        if (!HasCount())
        {
            _counted.Text = "—";
            _varianceCaption.Text = "Cash Difference:";
            _variance.Text = "—";
            _variance.ForeColor = _varianceCaption.ForeColor = Theme.Muted;
            return;
        }

        var counted = lines.Sum(l => l.Amount);
        _counted.Text = $"{_currency} {Ui.Money(counted)}";
        if (_report is null) return;

        var variance = counted - _report.ExpectedCash;
        _varianceCaption.Text = $"{VarianceLabel(variance)}:";
        _variance.Text = $"{_currency} {Ui.Money(Math.Abs(variance))}";
        _variance.ForeColor = _varianceCaption.ForeColor = variance == 0 ? Theme.Success : variance < 0 ? Theme.Danger : Theme.Warning;
    }

    private async Task CloseDayAsync()
    {
        if (_report is null) return;
        _count.EndEdit();
        UpdateCash();

        if (!HasCount())
        {
            Ui.Warn(this, "Please count the cash in the drawer.\n\nType the number of notes in the yellow 'Count' box next to each denomination (e.g. 5,000 x 2).\nIf the drawer is empty, enter 0 in any row.");
            _count.Focus();
            _count.CurrentCell = _count.Rows[0].Cells["Count"];
            _count.BeginEdit(true);
            return;
        }

        var lines = CountLines();
        var counted = lines.Sum(l => l.Amount);
        var variance = counted - _report.ExpectedCash;
        if (variance != 0 && string.IsNullOrWhiteSpace(_notes.Text))
        {
            Ui.Warn(this, $"{VarianceLabel(variance)}: {_currency} {Ui.Money(Math.Abs(variance))}\n\nPlease enter a reason in the closing notes.");
            _notes.Focus();
            return;
        }

        var message = $"Close business day {_report.BusinessDate:dd-MMM-yyyy}?\n\n" +
                      $"Net Sales:       {_currency} {Ui.Money(_report.NetSales)}\n" +
                      $"Expected Cash: {_currency} {Ui.Money(_report.ExpectedCash)}\n" +
                      $"Counted Cash:  {_currency} {Ui.Money(counted)}\n" +
                      $"{VarianceLabel(variance)}: {_currency} {Ui.Money(Math.Abs(variance))}\n\n" +
                      "No further orders can be taken until a new day is opened.";
        if (!Ui.Confirm(this, message)) return;

        await this.RunAsync(async () =>
        {
            ClosedReport = await _days.CloseAsync(counted, _notes.Text, lines);
            await PrintAsync(ClosedReport);
            DialogResult = DialogResult.OK;
        });
    }

    private async Task PrintAsync(DayReport? report)
    {
        if (report is null) return;
        try
        {
            await _printer.PrintDayReportAsync(report);
        }
        catch (Exception ex)
        {
            Ui.Warn(this, $"The day report could not be printed. You can reprint it from Reports > Day Closings.\n\n{ex.Message}");
        }
    }

    private static string VarianceLabel(decimal difference) =>
        difference == 0 ? "Cash Balanced" : difference < 0 ? "Cash Short" : "Cash Over";

    private static string Duration(TimeSpan span) =>
        span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m" : $"{Math.Max(0, span.Minutes)}m";

    private static Label Caption(string text, float size = 10.5f, FontStyle style = FontStyle.Regular, Color? color = null) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = Theme.Font(size, style),
        ForeColor = color ?? Theme.Muted,
        Margin = Padding.Empty
    };

    private static Label Value(float size = 11f) => new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight,
        Font = Theme.Font(size, FontStyle.Bold),
        ForeColor = Theme.Text,
        Margin = Padding.Empty
    };
}
