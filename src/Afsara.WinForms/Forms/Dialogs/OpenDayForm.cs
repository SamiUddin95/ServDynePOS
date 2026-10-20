using Afsara.Application.Common;
using Afsara.Application.Days;
using Afsara.Application.Settings;
using Afsara.Domain.Entities;
using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Dialogs;

public class OpenDayForm : Form
{
    private readonly IBusinessDayService _days;
    private readonly ISettingsService _settings;
    private readonly NumericUpDown _opening = Theme.Numeric(100_000_000);
    private readonly TextBox _notes = Theme.TextBox("Optional note (e.g. float received from manager)");
    private readonly Label _currency = Theme.Label(string.Empty, 16f, FontStyle.Bold, Theme.Muted);
    private readonly Label _lastClose = Theme.Label(string.Empty, 9.5f, FontStyle.Regular, Theme.Muted);
    private readonly Button _useLast = Theme.OutlineButton("Use last closing cash", 9.5f);
    private BusinessDay? _last;

    public BusinessDay? OpenedDay { get; private set; }

    public OpenDayForm(IBusinessDayService days, ISettingsService settings, IUserSession session)
    {
        _days = days;
        _settings = settings;
        Theme.ApplyDialog(this, "Open Business Day", new Size(520, 470));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Header, Margin = Padding.Empty, Padding = new Padding(20, 14, 20, 10) };
        header.Controls.Add(new Label
        {
            Text = $"{DateTime.Today:dddd, dd MMMM yyyy}   |   {session.RequireUser().FullName}",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = Theme.Font(10.5f)
        });
        header.Controls.Add(new Label { Text = "OPEN BUSINESS DAY", Dock = DockStyle.Top, Height = 40, ForeColor = Color.White, Font = Theme.Font(17f, FontStyle.Bold) });
        root.Controls.Add(header, 0, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20, 14, 20, 14), BackColor = Theme.Surface, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        foreach (var height in new float[] { 30, 40, 34, 64, 30, 40 })
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.Controls.Add(body, 0, 1);

        void Span(Control control, int row)
        {
            body.Controls.Add(control, 0, row);
            body.SetColumnSpan(control, 2);
        }

        _lastClose.AutoSize = false;
        _lastClose.Dock = DockStyle.Fill;
        _lastClose.TextAlign = ContentAlignment.MiddleLeft;
        Span(_lastClose, 0);
        _useLast.Visible = false;
        _useLast.Click += (_, _) => { if (_last is not null) _opening.Value = Math.Min(_last.CountedCash, _opening.Maximum); };
        Span(_useLast, 1);

        Span(Theme.Label("Opening Cash Balance (float in drawer)", 11f, FontStyle.Bold), 2);

        var amount = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        amount.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        amount.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _currency.Anchor = AnchorStyles.Left;
        amount.Controls.Add(_currency, 0, 0);
        _opening.Font = Theme.Font(22f, FontStyle.Bold);
        _opening.TextAlign = HorizontalAlignment.Right;
        amount.Controls.Add(_opening, 1, 0);
        Span(amount, 3);

        Span(Theme.Label("Notes", 10.5f, FontStyle.Bold), 4);
        Span(_notes, 5);

        var cancel = Theme.OutlineButton("Not Now (Esc)", 10.5f);
        cancel.DialogResult = DialogResult.Cancel;
        var open = Theme.Button("OPEN DAY (Enter)", Theme.Success, 12f);
        open.Click += async (_, _) => await OpenAsync();
        body.Controls.Add(cancel, 0, 7);
        body.Controls.Add(open, 1, 7);

        AcceptButton = open;
        CancelButton = cancel;
        Load += async (_, _) => await this.RunAsync(LoadAsync);
        Shown += (_, _) => { _opening.Focus(); _opening.Select(0, _opening.Text.Length); };
    }

    private async Task LoadAsync()
    {
        var settings = await _settings.GetAsync();
        _currency.Text = settings.CurrencySymbol;
        _last = await _days.GetLastClosedAsync();
        if (_last is null)
        {
            _lastClose.Text = "No previous day closing found. Enter the cash available in the drawer.";
            return;
        }

        _lastClose.Text = $"Last day {_last.BusinessDate:dd-MMM-yyyy} was closed on {_last.ClosedAt:dd-MMM hh:mm tt}";
        _useLast.Text = $"Use last closing cash ({settings.CurrencySymbol} {Ui.Money(_last.CountedCash)})";
        _useLast.Visible = _last.CountedCash > 0;
    }

    private async Task OpenAsync()
    {
        if (_opening.Value == 0 && !Ui.Confirm(this, "Open the day with a zero opening balance?")) return;
        await this.RunAsync(async () =>
        {
            OpenedDay = await _days.OpenAsync(_opening.Value, _notes.Text);
            DialogResult = DialogResult.OK;
        });
    }
}
