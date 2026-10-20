using System.Diagnostics;
using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Application.Days;
using Afsara.Application.Orders;
using Afsara.Application.Reports;
using Afsara.Application.Settings;
using Afsara.Domain.Enums;
using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Admin;

public class ReportsForm : Form
{
    private readonly IReportService _reports;
    private readonly IOrderService _orders;
    private readonly IReceiptPrinter _printer;
    private readonly IReportExporter _exporter;
    private readonly ISettingsService _settings;

    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 140, Font = Theme.Font(11f) };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 140, Font = Theme.Font(11f) };
    private readonly FlowLayoutPanel _cards = new() { Dock = DockStyle.Fill, Padding = new Padding(4), AutoScroll = true };
    private readonly DataGridView _ordersGrid = new();
    private readonly DataGridView _itemsGrid = new();
    private readonly DataGridView _daysGrid = new();
    private readonly IBusinessDayService _days;
    private readonly ComboBox _day = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, Font = Theme.Font(10.5f) };
    private readonly Label _dayInfo = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Theme.Font(10.5f, FontStyle.Bold), Padding = new Padding(12, 0, 0, 0) };
    private bool _daySuppress;
    private SalesReport? _report;

    private readonly DateTimePicker _salesFrom = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 140, Font = Theme.Font(11f) };
    private readonly DateTimePicker _salesTo = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 140, Font = Theme.Font(11f) };
    private DateTime _businessDate = DateTime.Today;
    private readonly Label _salesInfo = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Theme.Font(10.5f, FontStyle.Bold), Padding = new Padding(12, 0, 0, 0), BackColor = Theme.Surface };
    private readonly FlowLayoutPanel _salesCards = new() { Dock = DockStyle.Fill, Padding = new Padding(2), WrapContents = false, AutoScroll = true };
    private readonly DataGridView _topItemsGrid = new();
    private readonly DataGridView _categoryGrid = new();
    private readonly DataGridView _hourlyGrid = new();
    private SalesReport? _daySalesReport;
    private DaySalesReport? _daySales;
    private DaySalesReport? _rangeSales;
    private readonly ComboBox _session = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, Font = Theme.Font(10.5f) };
    private readonly DataGridView _sessionsGrid = new();
    private bool _sessionSuppress;
    private bool _showAllSessions;

    private const string AllPayments = "All Payments";
    private const string NotPaid = "Not Paid (Cancelled)";
    private readonly ComboBox _payment = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Font = Theme.Font(10.5f) };
    private readonly Label _ordersSummary = new() { AutoSize = true, Font = Theme.Font(10.5f, FontStyle.Bold), ForeColor = Theme.Muted, Margin = new Padding(16, 8, 3, 3) };

    private sealed record DayItem(int? DayId, string Text)
    {
        public override string ToString() => Text;
    }

    public ReportsForm(IReportService reports, IOrderService orders, IReceiptPrinter printer, IReportExporter exporter, ISettingsService settings, IBusinessDayService days)
    {
        _days = days;
        _reports = reports;
        _orders = orders;
        _printer = printer;
        _exporter = exporter;
        _settings = settings;

        Theme.ApplyDialog(this, "Sales Reports", new Size(1320, 760));
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimumSize = new Size(1000, 600);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var filter = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(8, 10, 8, 0), WrapContents = false };
        filter.Controls.Add(Theme.Label("From", 10.5f, FontStyle.Bold));
        filter.Controls.Add(_from);
        filter.Controls.Add(Theme.Label("To", 10.5f, FontStyle.Bold));
        filter.Controls.Add(_to);
        filter.Controls.Add(FilterButton("Today", Theme.Gray, () => SetRange(DateTime.Today, DateTime.Today)));
        filter.Controls.Add(FilterButton("Yesterday", Theme.Gray, () => SetRange(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(-1))));
        filter.Controls.Add(FilterButton("This Month", Theme.Gray, () => SetRange(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today)));
        filter.Controls.Add(Theme.Label("Business Day", 10.5f, FontStyle.Bold));
        filter.Controls.Add(_day);
        filter.Controls.Add(FilterButton("Show Report", Theme.Primary, () => _ = LoadReportAsync()));
        filter.Controls.Add(FilterButton("Print Summary", Theme.Purple, () => _ = PrintSummaryAsync()));
        filter.Controls.Add(FilterButton("Export Excel", Theme.Success, () => _ = ExportAsync(pdf: false)));
        filter.Controls.Add(FilterButton("Export PDF", Theme.Danger, () => _ = ExportAsync(pdf: true)));
        root.Controls.Add(filter, 0, 0);

        _dayInfo.BackColor = Theme.Surface;
        _dayInfo.Margin = new Padding(0, 2, 0, 2);
        root.Controls.Add(_dayInfo, 0, 1);
        ShowDayInfo(null);

        _day.SelectedIndexChanged += async (_, _) =>
        {
            if (_daySuppress) return;
            if (_day.SelectedItem is DayItem { DayId: int dayId }) await LoadBusinessDayAsync(dayId);
            else await LoadReportAsync();
        };

        root.Controls.Add(_cards, 0, 2);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.Font(10.5f, FontStyle.Bold), Padding = new Point(16, 6) };
        var ordersPage = new TabPage("Orders") { BackColor = Theme.Surface };
        Theme.StyleGrid(_ordersGrid);
        _ordersGrid.Font = Theme.Font(10f);
        ordersPage.Controls.Add(_ordersGrid);
        var ordersBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(4), FlowDirection = FlowDirection.RightToLeft };
        ordersBar.Controls.Add(FilterButton("Reprint Invoice", Theme.Success, () => _ = ReprintAsync()));
        ordersPage.Controls.Add(ordersBar);
        var ordersFilter = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 6, 8, 0), WrapContents = false, BackColor = Theme.Background };
        ordersFilter.Controls.Add(Theme.Label("Payment", 10.5f, FontStyle.Bold));
        ordersFilter.Controls.Add(_payment);
        ordersFilter.Controls.Add(_ordersSummary);
        ordersPage.Controls.Add(ordersFilter);
        _payment.Items.Add(AllPayments);
        foreach (var method in Enum.GetValues<PaymentMethod>()) _payment.Items.Add(method.ToDisplay());
        _payment.Items.Add(NotPaid);
        _payment.SelectedIndex = 0;
        _payment.SelectedIndexChanged += (_, _) => BindOrders();
        _ordersGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) _ = ReprintAsync(); };
        tabs.TabPages.Add(ordersPage);

        var itemsPage = new TabPage("Item-wise Sales") { BackColor = Theme.Surface };
        Theme.StyleGrid(_itemsGrid);
        _itemsGrid.Font = Theme.Font(10f);
        itemsPage.Controls.Add(_itemsGrid);
        tabs.TabPages.Add(itemsPage);

        var daysPage = new TabPage("Day Closings") { BackColor = Theme.Surface };
        Theme.StyleGrid(_daysGrid);
        _daysGrid.Font = Theme.Font(10f);
        daysPage.Controls.Add(_daysGrid);
        var daysBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(4), FlowDirection = FlowDirection.RightToLeft };
        daysBar.Controls.Add(FilterButton("Reprint Z-Report", Theme.Success, () => _ = ReprintDayAsync()));
        daysPage.Controls.Add(daysBar);
        _daysGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) _ = ReprintDayAsync(); };
        _daysGrid.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewRow row in _daysGrid.Rows)
            {
                var status = Convert.ToString(row.Cells["Status"].Value);
                row.Cells["Status"].Style.ForeColor = status == "Balanced" ? Theme.Success : status?.StartsWith("Short") == true ? Theme.Danger : Theme.Warning;
            }
        };
        tabs.TabPages.Add(daysPage);

        var salesPage = BuildDaySalesPage();
        tabs.TabPages.Add(salesPage);
        tabs.SelectedIndexChanged += (_, _) =>
        {
            var focus = tabs.SelectedTab == salesPage;
            root.RowStyles[1].Height = focus ? 0 : 36;
            root.RowStyles[2].Height = focus ? 0 : 190;
            _dayInfo.Visible = _cards.Visible = !focus;
        };
        root.Controls.Add(tabs, 0, 3);

        _from.Value = DateTime.Today;
        _to.Value = DateTime.Today;
        Load += async (_, _) =>
        {
            await LoadDayPickerAsync();
            await LoadReportAsync();
        };
    }

    private TabPage BuildDaySalesPage()
    {
        var page = new TabPage("Day Sales") { BackColor = Theme.Background, Padding = new Padding(0, 6, 0, 0) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        page.Controls.Add(layout);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(8, 8, 8, 0), WrapContents = false, Margin = new Padding(0, 0, 0, 2) };
        bar.Controls.Add(Theme.Label("From", 10.5f, FontStyle.Bold));
        bar.Controls.Add(_salesFrom);
        bar.Controls.Add(Theme.Label("To", 10.5f, FontStyle.Bold));
        bar.Controls.Add(_salesTo);
        bar.Controls.Add(FilterButton("Today", Theme.Gray, () => SetSalesRange(_businessDate, _businessDate)));
        bar.Controls.Add(FilterButton("Yesterday", Theme.Gray, () => SetSalesRange(_businessDate.AddDays(-1), _businessDate.AddDays(-1))));
        bar.Controls.Add(FilterButton("Show", Theme.Primary, () => _ = LoadDaySalesAsync()));
        var sessionLabel = Theme.Label("Day Session", 10.5f, FontStyle.Bold);
        sessionLabel.Margin = new Padding(18, 6, 3, 3);
        bar.Controls.Add(sessionLabel);
        bar.Controls.Add(_session);
        bar.AutoScroll = true;
        layout.Controls.Add(bar, 0, 0);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(4, 7, 4, 0), FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        actions.Controls.Add(FilterButton("Export PDF", Theme.Danger, () => _ = ExportDaySalesAsync(pdf: true)));
        actions.Controls.Add(FilterButton("Export Excel", Theme.Success, () => _ = ExportDaySalesAsync(pdf: false)));
        actions.Controls.Add(FilterButton("Print Summary", Theme.Purple, () => _ = PrintDaySummaryAsync()));
        actions.Controls.Add(FilterButton("Print Day Sales", Theme.Warning, () => _ = PrintDaySalesAsync()));
        layout.Controls.Add(actions, 0, 4);

        _session.SelectedIndexChanged += (_, _) =>
        {
            if (_sessionSuppress) return;
            _showAllSessions = _session.SelectedItem is DayItem { DayId: null };
            _ = this.RunAsync(ShowSelectedSessionAsync);
        };
        _sessionsGrid.CellClick += (_, e) =>
        {
            if (e.RowIndex < 0 || _sessionsGrid.Rows[e.RowIndex].Cells["Id"].Value is not int id) return;
            var item = _session.Items.Cast<DayItem>().FirstOrDefault(i => i.DayId == id);
            if (item is not null && !ReferenceEquals(item, _session.SelectedItem)) _session.SelectedItem = item;
        };
        _sessionsGrid.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewRow row in _sessionsGrid.Rows)
                row.Cells["Status"].Style.ForeColor = Convert.ToString(row.Cells["Status"].Value) == "Closed" ? Theme.Success : Theme.Warning;
        };

        _salesInfo.Margin = new Padding(0, 2, 0, 2);
        _salesInfo.ForeColor = Theme.Muted;
        _salesInfo.Text = "Pick a business date range to see everything sold between day open and day close.";
        layout.Controls.Add(_salesInfo, 0, 1);
        layout.Controls.Add(_salesCards, 0, 2);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Margin = new Padding(0) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        var top = Section("TOP SELLING ITEMS  -  ranked by quantity sold", _topItemsGrid, Theme.Success);
        body.Controls.Add(top, 0, 0);
        body.SetRowSpan(top, 3);
        body.Controls.Add(Section("DAY SESSIONS  -  click a session (day open to day close)", _sessionsGrid, Theme.Warning), 1, 0);
        body.Controls.Add(Section("CATEGORY SUMMARY", _categoryGrid, Theme.Purple), 1, 1);
        body.Controls.Add(Section("HOURLY SALES", _hourlyGrid, Theme.Primary), 1, 2);
        layout.Controls.Add(body, 0, 3);

        foreach (var grid in new[] { _topItemsGrid, _sessionsGrid, _categoryGrid, _hourlyGrid })
        {
            Theme.StyleGrid(grid);
            grid.Font = Theme.Font(10f);
            grid.DefaultCellStyle.Font = Theme.Font(10f);
            grid.RowTemplate.Height = 32;
        }
        AddShareBars(_topItemsGrid, "Share", Theme.Success);
        AddShareBars(_categoryGrid, "Share", Theme.Purple);
        AddShareBars(_hourlyGrid, "Sales", Theme.Primary);
        _topItemsGrid.DataBindingComplete += (_, _) => HighlightTopSellers();

        return page;
    }

    private static Control Section(string title, DataGridView grid, Color accent)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Margin = new Padding(3), Padding = new Padding(1) };
        panel.Controls.Add(grid);
        panel.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 34,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = Theme.Font(10f, FontStyle.Bold),
            ForeColor = accent,
            Padding = new Padding(10, 0, 0, 0)
        });
        panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 3, BackColor = accent });
        return panel;
    }

    private static void AddShareBars(DataGridView grid, string column, Color color)
    {
        decimal max = 0;
        grid.DataBindingComplete += (_, _) =>
        {
            max = grid.Columns.Contains(column)
                ? grid.Rows.Cast<DataGridViewRow>().Select(r => r.Cells[column].Value is decimal d ? d : 0).DefaultIfEmpty(0).Max()
                : 0;
        };
        grid.CellPainting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != column || e.Value is not decimal value || e.Graphics is null) return;
            e.PaintBackground(e.CellBounds, true);
            if (max > 0 && value > 0)
            {
                var area = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + 8, e.CellBounds.Width - 12, e.CellBounds.Height - 16);
                using var track = new SolidBrush(Theme.Background);
                using var fill = new SolidBrush(Color.FromArgb(90, color));
                e.Graphics.FillRectangle(track, area);
                e.Graphics.FillRectangle(fill, area with { Width = Math.Max(2, (int)(area.Width * (value / max))) });
            }
            e.PaintContent(e.CellBounds);
            e.Handled = true;
        };
    }

    private void HighlightTopSellers()
    {
        var medals = new[] { Color.FromArgb(250, 204, 21), Color.FromArgb(203, 213, 225), Color.FromArgb(251, 146, 60) };
        var bold = Theme.Font(10f, FontStyle.Bold);
        for (var i = 0; i < Math.Min(medals.Length, _topItemsGrid.Rows.Count); i++)
        {
            var row = _topItemsGrid.Rows[i];
            row.DefaultCellStyle.Font = bold;
            row.Cells["Rank"].Style.BackColor = medals[i];
            row.Cells["Rank"].Style.SelectionBackColor = medals[i];
        }
    }

    private static void FormatColumns(DataGridView grid, int? fillWeightFirst = null, params string[] numeric)
    {
        foreach (var name in numeric)
        {
            if (grid.Columns[name] is not { } col) continue;
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            if (col.ValueType == typeof(decimal) && string.IsNullOrEmpty(col.DefaultCellStyle.Format)) col.DefaultCellStyle.Format = "N0";
        }
        if (fillWeightFirst is int weight && grid.Columns.Count > 1) grid.Columns[1].FillWeight = weight;
    }

    private void InitSalesRange(IReadOnlyList<DayReport> days)
    {
        _businessDate = days.FirstOrDefault(d => !d.IsClosed)?.BusinessDate.Date ?? DateTime.Today;
        SetSalesRange(_businessDate, _businessDate);
    }

    private void SetSalesRange(DateTime from, DateTime to, int? dayId = null)
    {
        _salesFrom.Value = from.Date;
        _salesTo.Value = to.Date;
        _ = LoadDaySalesAsync(dayId);
    }

    private async Task LoadDaySalesAsync(int? preferDayId = null)
    {
        if (_salesFrom.Value.Date > _salesTo.Value.Date)
        {
            Ui.Warn(this, "'From' date cannot be after 'To' date.");
            return;
        }

        var from = _salesFrom.Value.Date;
        var to = _salesTo.Value.Date;
        await this.RunAsync(async () =>
        {
            var range = await _reports.GetDaySalesAsync(from, to);
            _rangeSales = range;
            var sessions = range.Sessions;

            var previous = (_session.SelectedItem as DayItem)?.DayId;
            int? target = preferDayId ?? (_showAllSessions ? null : previous);
            if (target is int t && sessions.All(s => s.DayId != t)) target = null;
            if (target is null && (!_showAllSessions || preferDayId is not null)) target = sessions.LastOrDefault()?.DayId;
            if (preferDayId is not null) _showAllSessions = false;

            _sessionSuppress = true;
            _session.Items.Clear();
            _session.Items.Add(new DayItem(null, sessions.Count > 1 ? $"All {sessions.Count} sessions combined" : "All sessions"));
            for (var i = 0; i < sessions.Count; i++)
                _session.Items.Add(new DayItem(sessions[i].DayId, SessionText(sessions[i], i + 1, range.SpansMultipleDates)));
            _session.SelectedItem = _session.Items.Cast<DayItem>().First(x => x.DayId == target);
            _sessionSuppress = false;

            _sessionsGrid.DataSource = sessions.Select((s, i) => new
            {
                Id = s.DayId,
                Session = $"#{i + 1}",
                Date = s.BusinessDate.ToString("dd-MMM"),
                Open = s.OpenedAt.ToString("hh:mm tt"),
                Close = s.IsClosed ? s.ClosedAt!.Value.ToString(s.ClosedAt.Value.Date == s.OpenedAt.Date ? "hh:mm tt" : "dd-MMM hh:mm tt") : "-",
                Orders = s.CompletedOrders,
                Sales = s.NetSales,
                Status = s.IsClosed ? "Closed" : "OPEN"
            }).ToList();
            if (_sessionsGrid.Columns["Id"] is { } id) id.Visible = false;
            FormatColumns(_sessionsGrid, null, "Orders", "Sales");

            await ShowSelectedSessionAsync();
        });
    }

    private static string SessionText(DaySessionRow s, int number, bool withDate)
    {
        var close = !s.IsClosed ? "OPEN"
            : s.ClosedAt!.Value.Date == s.OpenedAt.Date ? s.ClosedAt.Value.ToString("hh:mm tt")
            : s.ClosedAt.Value.ToString("dd-MMM hh:mm tt");
        return withDate
            ? $"#{number}   {s.BusinessDate:dd-MMM}   {s.OpenedAt:hh:mm tt} - {close}"
            : $"Session #{number}   {s.OpenedAt:hh:mm tt} - {close}";
    }

    private async Task ShowSelectedSessionAsync()
    {
        if (_rangeSales is not { } range) return;
        string? caption = null;
        if (_session.SelectedItem is DayItem { DayId: int dayId })
        {
            _daySales = await _reports.GetDaySalesAsync(dayId);
            _daySalesReport = await _reports.GetSalesReportForDayAsync(dayId);
            var index = range.Sessions.FindIndex(s => s.DayId == dayId);
            caption = $"Session #{index + 1} of {range.Sessions.Count}";
        }
        else
        {
            _daySales = range;
            _daySalesReport = await _reports.GetSalesReportAsync(range.From, range.To);
            if (range.Sessions.Count > 1) caption = $"All {range.Sessions.Count} sessions combined";
        }
        ShowDaySales(_daySales, caption);

        var selectedId = (_session.SelectedItem as DayItem)?.DayId;
        _sessionsGrid.ClearSelection();
        foreach (DataGridViewRow row in _sessionsGrid.Rows)
            if (selectedId is not null && row.Cells["Id"].Value is int rowId && rowId == selectedId)
            {
                row.Selected = true;
                _sessionsGrid.CurrentCell = row.Cells["Session"];
            }
    }

    private void ShowDaySales(DaySalesReport? s, string? caption = null)
    {
        _salesCards.SuspendLayout();
        var old = _salesCards.Controls.Cast<Control>().ToList();
        _salesCards.Controls.Clear();
        old.ForEach(c => c.Dispose());

        if (s is null)
        {
            _daySalesReport = null;
            _daySales = null;
            _salesInfo.Text = "No business days found. Open a day to start recording sales.";
            _salesInfo.ForeColor = Theme.Muted;
            _topItemsGrid.DataSource = _categoryGrid.DataSource = _hourlyGrid.DataSource = null;
            _salesCards.ResumeLayout();
            return;
        }

        var sessions = s.Sessions;
        if (sessions.Count == 0)
        {
            _salesInfo.Text = $"{s.Period}   |   No business day was opened in this period.";
            _salesInfo.ForeColor = Theme.Muted;
        }
        else
        {
            var first = sessions[0];
            var last = sessions[^1];
            var span = TimeSpan.FromTicks(sessions.Sum(d => ((d.ClosedAt ?? DateTime.Now) - d.OpenedAt).Ticks));
            var duration = $"{(int)span.TotalHours}h {span.Minutes:D2}m";
            var close = last.IsClosed ? $"Day Close {last.ClosedAt:dd-MMM hh:mm tt} by {last.ClosedBy}" : $"DAY OPEN - live figures as of {DateTime.Now:hh:mm tt}";
            var title = s.SpansMultipleDates ? $"{s.Period}  ({sessions.Count} sessions)" : $"{s.From:dddd, dd-MMM-yyyy}";
            if (caption is not null) title = $"{caption}   |   {title}";
            _salesInfo.Text = $"{title}   |   Day Open {first.OpenedAt:dd-MMM hh:mm tt} by {first.OpenedBy}   |   {close}   |   Trading time {duration}";
            _salesInfo.ForeColor = s.IsClosed ? Theme.Success : Theme.Warning;
        }
        var timeFormat = s.SpansMultipleDates ? "dd-MMM hh:mm tt" : "hh:mm tt";

        var topItem = s.TopItem;
        var peak = s.PeakHour;
        _salesCards.Controls.Add(Card("Net Sales", Ui.Money(s.NetSales), Theme.Success, true));
        _salesCards.Controls.Add(Card("Orders", s.CompletedOrders.ToString(), Theme.Primary));
        _salesCards.Controls.Add(Card("Items Sold", s.ItemsSold.ToString("N0"), Theme.Teal));
        _salesCards.Controls.Add(Card("Unique Items", s.Items.Count.ToString(), Theme.Purple));
        _salesCards.Controls.Add(Card("Avg Order", Ui.Money(s.AverageOrder), Theme.Gray));
        _salesCards.Controls.Add(Card(topItem is null ? "Top Seller" : $"Top Seller  -  {topItem.Quantity} sold", topItem?.Item ?? "-", Theme.Warning, width: 260));
        _salesCards.Controls.Add(Card(peak is null ? "Peak Hour" : $"Peak Hour  -  {peak.Orders} orders", peak?.Label ?? "-", Theme.Primary, width: 210));
        _salesCards.Controls.Add(Card("Cancelled", s.CancelledOrders.ToString(), Theme.Danger));
        _salesCards.ResumeLayout();

        _topItemsGrid.DataSource = s.Items.Select(i => new
        {
            Rank = $"#{i.Rank}",
            i.Item,
            i.Category,
            Qty = i.Quantity,
            i.Orders,
            AvgPrice = i.AveragePrice,
            Amount = i.Amount,
            Share = i.QuantityShare,
            First = i.FirstSold.ToString(timeFormat),
            Last = i.LastSold.ToString(timeFormat)
        }).ToList();
        FormatColumns(_topItemsGrid, 220, "Qty", "Orders", "AvgPrice", "Amount", "Share");
        if (_topItemsGrid.Columns["Rank"] is { } rank)
        {
            rank.FillWeight = 45;
            rank.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
        if (_topItemsGrid.Columns["AvgPrice"] is { } avg) avg.HeaderText = "Avg Price";
        if (_topItemsGrid.Columns["Share"] is { } share)
        {
            share.HeaderText = "Qty Share";
            share.DefaultCellStyle.Format = "0.0'%'";
            share.FillWeight = 130;
        }

        _categoryGrid.DataSource = s.Categories.Select(c => new
        {
            c.Category,
            c.Items,
            Qty = c.Quantity,
            c.Amount,
            Share = c.AmountShare
        }).ToList();
        FormatColumns(_categoryGrid, null, "Items", "Qty", "Amount", "Share");
        if (_categoryGrid.Columns["Share"] is { } catShare)
        {
            catShare.HeaderText = "Sales Share";
            catShare.DefaultCellStyle.Format = "0.0'%'";
        }

        _hourlyGrid.DataSource = s.Hours.Select(h => new
        {
            Hour = h.Label,
            h.Orders,
            Items = h.Quantity,
            Sales = h.Amount
        }).ToList();
        FormatColumns(_hourlyGrid, null, "Orders", "Items", "Sales");
        if (_hourlyGrid.Columns["Hour"] is { } hour) hour.FillWeight = 150;
    }

    private async Task ExportDaySalesAsync(bool pdf)
    {
        if (_daySales is not { } report || _daySalesReport is not { } sales) return;
        var range = report.SpansMultipleDates ? $"{report.From:yyyyMMdd}_{report.To:yyyyMMdd}" : $"{report.From:yyyyMMdd}";
        if (_session.SelectedItem is DayItem { DayId: not null } && report.Sessions.Count == 1) range += $"_Session_{report.Sessions[0].OpenedAt:HHmm}";

        using var dialog = new SaveFileDialog
        {
            Title = pdf ? "Export Day Sales to PDF" : "Export Day Sales to Excel",
            Filter = pdf ? "PDF Document|*.pdf" : "Excel Workbook|*.xlsx",
            FileName = $"Day_Sales_{range}{(pdf ? ".pdf" : ".xlsx")}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var path = dialog.FileName;

        await this.RunAsync(async () =>
        {
            var settings = await _settings.GetAsync();
            await Task.Run(() =>
            {
                if (pdf) _exporter.ExportDaySalesPdf(report, sales, settings, path);
                else _exporter.ExportDaySalesExcel(report, sales, settings, path);
            });

            if (Ui.Confirm(this, $"Day sales exported successfully:\n{path}\n\nOpen it now?"))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        });
    }

    private async Task PrintDaySalesAsync()
    {
        if (_daySales is null)
        {
            Ui.Warn(this, "Pick a business date range to print its sales report.");
            return;
        }
        await this.RunAsync(() => _printer.PrintDaySalesAsync(_daySales));
    }

    private async Task PrintDaySummaryAsync()
    {
        if (_daySalesReport is null) return;
        await this.RunAsync(() => _printer.PrintSalesSummaryAsync(_daySalesReport));
    }

    private static Button FilterButton(string text, Color color, Action onClick)
    {
        var button = Theme.Button(text, color, 9.5f);
        button.Dock = DockStyle.None;
        button.Size = new Size(text.Length > 10 ? 130 : 100, 34);
        button.Margin = new Padding(6, 0, 0, 0);
        button.Click += (_, _) => onClick();
        return button;
    }

    private void SetRange(DateTime from, DateTime to)
    {
        _from.Value = from;
        _to.Value = to;
        _ = LoadReportAsync();
    }

    private async Task LoadDayPickerAsync()
    {
        var days = await _days.GetDaysAsync(DateTime.Today.AddDays(-180), DateTime.Today);
        _daySuppress = true;
        _day.Items.Clear();
        _day.Items.Add(new DayItem(null, "Date Range"));
        foreach (var d in days)
        {
            var close = d.IsClosed ? d.ClosedAt!.Value.ToString("hh:mm tt") : "OPEN";
            _day.Items.Add(new DayItem(d.DayId, $"{d.BusinessDate:dd-MMM-yyyy}   {d.OpenedAt:hh:mm tt} - {close}"));
        }
        _day.SelectedIndex = 0;
        _daySuppress = false;
        InitSalesRange(days);
    }

    private void ResetDayPicker()
    {
        ShowDayInfo(null);
        if (_day.SelectedIndex == 0) return;
        _daySuppress = true;
        _day.SelectedIndex = 0;
        _daySuppress = false;
    }

    private void ShowDayInfo(DayReport? d)
    {
        if (d is null)
        {
            _dayInfo.Text = "Date range report  -  pick a Business Day above to see a single open/close cycle.";
            _dayInfo.ForeColor = Theme.Muted;
            return;
        }

        var status = d.IsClosed
            ? $"Closed {d.ClosedAt:dd-MMM hh:mm tt} by {d.ClosedBy}"
            : $"OPEN since {d.OpenedAt:hh:mm tt}";
        var cash = d.IsClosed
            ? $"Opening {Ui.Money(d.OpeningBalance)}  |  Expected {Ui.Money(d.ExpectedCash)}  |  Counted {(d.CountedCash is null ? "-" : Ui.Money(d.CountedCash.Value))}  |  {VarianceText(d.CashVariance)}"
            : $"Opening {Ui.Money(d.OpeningBalance)}  |  Expected cash {Ui.Money(d.ExpectedCash)}  |  Running orders {d.RunningOrders} ({Ui.Money(d.RunningAmount)})";

        _dayInfo.Text = $"Business Day {d.BusinessDate:dddd, dd-MMM-yyyy}   |   Opened {d.OpenedAt:hh:mm tt} by {d.OpenedBy}   |   {status}   |   {cash}";
        _dayInfo.ForeColor = !d.IsClosed ? Theme.Warning
            : (d.CashVariance ?? 0) == 0 ? Theme.Success
            : d.CashVariance < 0 ? Theme.Danger : Theme.Primary;
    }

    private static string VarianceText(decimal? variance) => variance switch
    {
        null => "-",
        0 => "Balanced",
        < 0 => $"Short {Ui.Money(-variance.Value)}",
        _ => $"Over {Ui.Money(variance.Value)}"
    };

    private void BindGrids(SalesReport report)
    {
        BindOrders();

        _itemsGrid.DataSource = report.Items.Select((i, index) => new
        {
            No = index + 1,
            i.Item,
            Quantity = i.Quantity,
            Amount = Ui.Money(i.Amount)
        }).ToList();
    }

    private void BindOrders()
    {
        var payment = _payment.SelectedItem as string ?? AllPayments;
        var orders = (_report?.Orders ?? new List<ReportOrderRow>()).Where(o => payment switch
        {
            AllPayments => true,
            NotPaid => o.Payment is null,
            _ => o.Payment == payment
        }).ToList();

        var completed = orders.Where(o => o.Status == "Completed").ToList();
        _ordersSummary.Text = $"{orders.Count} order(s)   |   Completed {completed.Count}   |   Total {Ui.Money(completed.Sum(o => o.Total))}";

        _ordersGrid.DataSource = orders.Select(o => new
        {
            o.Id,
            Invoice = o.Invoice,
            Order = $"#{o.OrderNo}",
            Date = o.Date.ToString("dd-MMM-yyyy hh:mm tt"),
            o.Type,
            Table = o.Table ?? "-",
            o.Status,
            Payment = o.Payment ?? "-",
            Total = Ui.Money(o.Total),
            o.Cashier
        }).ToList();
        if (_ordersGrid.Columns["Id"] is { } id) id.Visible = false;
    }

    private async Task LoadBusinessDayAsync(int dayId)
    {
        await this.RunAsync(async () =>
        {
            var day = await _days.GetReportAsync(dayId);
            _report = await _reports.GetSalesReportForDayAsync(dayId);
            ShowDayInfo(day);
            RenderCards(_report);
            BindGrids(_report);
            SetSalesRange(day.BusinessDate, day.BusinessDate, day.DayId);
        });
    }

    private async Task LoadReportAsync()
    {
        if (_from.Value.Date > _to.Value.Date)
        {
            Ui.Warn(this, "'From' date cannot be after 'To' date.");
            return;
        }

        await this.RunAsync(async () =>
        {
            ResetDayPicker();
            _report = await _reports.GetSalesReportAsync(_from.Value, _to.Value);
            RenderCards(_report);
            BindGrids(_report);

            var closings = await _days.GetClosedDaysAsync(_from.Value, _to.Value);
            _daysGrid.DataSource = closings.Select(d => new
            {
                Id = d.DayId,
                Date = d.BusinessDate.ToString("dd-MMM-yyyy"),
                Opened = $"{d.OpenedAt:dd-MMM hh:mm tt}  {d.OpenedBy}",
                Closed = $"{d.ClosedAt:dd-MMM hh:mm tt}  {d.ClosedBy}",
                Orders = d.CompletedOrders,
                NetSales = Ui.Money(d.NetSales),
                Opening = Ui.Money(d.OpeningBalance),
                CashSales = Ui.Money(d.CashSales),
                Expected = Ui.Money(d.ExpectedCash),
                Counted = Ui.Money(d.CountedCash ?? 0),
                Status = VarianceText(d.CashVariance),
                Notes = d.ClosingNotes ?? string.Empty
            }).ToList();
            if (_daysGrid.Columns["Id"] is { } dayId) dayId.Visible = false;
        });
    }

    private async Task ReprintDayAsync()
    {
        if (_daysGrid.CurrentRow?.Cells["Id"].Value is not int id) return;
        await this.RunAsync(async () =>
        {
            var report = await _days.GetReportAsync(id);
            await _printer.PrintDayReportAsync(report, duplicate: true);
        });
    }

    private void RenderCards(SalesReport r)
    {
        _cards.SuspendLayout();
        var old = _cards.Controls.Cast<Control>().ToList();
        _cards.Controls.Clear();
        old.ForEach(c => c.Dispose());

        _cards.Controls.Add(Card("NET SALES", Ui.Money(r.NetSales), Theme.Success, true));
        _cards.Controls.Add(Card("Orders", r.CompletedOrders.ToString(), Theme.Primary));
        _cards.Controls.Add(Card("Gross Sales", Ui.Money(r.GrossSales), Theme.Teal));
        _cards.Controls.Add(Card("Discounts", Ui.Money(r.Discounts), Theme.Danger));
        _cards.Controls.Add(Card("Sales Tax", Ui.Money(r.Taxes), Theme.Purple));
        _cards.Controls.Add(Card("Service Charges", Ui.Money(r.ServiceCharges), Theme.Gray));
        _cards.Controls.Add(Card("Cash", Ui.Money(r.CashSales), Theme.Success));
        _cards.Controls.Add(Card("Card", Ui.Money(r.CardSales), Theme.Primary));
        _cards.Controls.Add(Card("Online", Ui.Money(r.OnlineSales), Theme.Teal));
        _cards.Controls.Add(Card("Dine In", Ui.Money(r.DineInSales), Theme.Warning));
        _cards.Controls.Add(Card("Take Away", Ui.Money(r.TakeAwaySales), Theme.Warning));
        _cards.Controls.Add(Card("Delivery", Ui.Money(r.DeliverySales), Theme.Warning));
        _cards.Controls.Add(Card("Cancelled", r.CancelledOrders.ToString(), Theme.Danger));
        _cards.ResumeLayout();
    }

    private static Control Card(string title, string value, Color accent, bool highlight = false, int width = 170)
    {
        var card = new Panel
        {
            Size = new Size(width, 84),
            Margin = new Padding(4),
            BackColor = highlight ? accent : Theme.Surface
        };
        card.Controls.Add(new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = Theme.Font(16f, FontStyle.Bold),
            ForeColor = highlight ? Color.White : Theme.Text,
            Padding = new Padding(10, 0, 0, 0)
        });
        card.Controls.Add(new Label
        {
            Text = title.ToUpperInvariant(),
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.BottomLeft,
            Font = Theme.Font(9f, FontStyle.Bold),
            ForeColor = highlight ? Color.White : accent,
            Padding = new Padding(10, 0, 0, 0)
        });
        card.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent });
        return card;
    }

    private async Task ReprintAsync()
    {
        if (_ordersGrid.CurrentRow?.Cells["Id"].Value is not int id) return;
        if ((string?)_ordersGrid.CurrentRow.Cells["Status"].Value != "Completed")
        {
            Ui.Warn(this, "Only completed orders can be reprinted.");
            return;
        }

        await this.RunAsync(async () =>
        {
            var invoice = await _orders.GetInvoiceAsync(id, duplicate: true);
            await _printer.PrintInvoiceAsync(invoice);
        });
    }

    private async Task PrintSummaryAsync()
    {
        if (_report is null) return;
        await this.RunAsync(() => _printer.PrintSalesSummaryAsync(_report));
    }

    private Task ExportAsync(bool pdf) => ExportAsync(_report, pdf);

    private async Task ExportAsync(SalesReport? report, bool pdf)
    {
        if (report is null) return;
        var range = report.From == report.To ? $"{report.From:yyyyMMdd}" : $"{report.From:yyyyMMdd}_{report.To:yyyyMMdd}";

        using var dialog = new SaveFileDialog
        {
            Title = pdf ? "Export Report to PDF" : "Export Report to Excel",
            Filter = pdf ? "PDF Document|*.pdf" : "Excel Workbook|*.xlsx",
            FileName = $"Sales_Report_{range}{(pdf ? ".pdf" : ".xlsx")}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var path = dialog.FileName;

        await this.RunAsync(async () =>
        {
            var settings = await _settings.GetAsync();
            await Task.Run(() =>
            {
                if (pdf) _exporter.ExportPdf(report, settings, path);
                else _exporter.ExportExcel(report, settings, path);
            });

            if (Ui.Confirm(this, $"Report exported successfully:\n{path}\n\nOpen it now?"))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        });
    }
}
