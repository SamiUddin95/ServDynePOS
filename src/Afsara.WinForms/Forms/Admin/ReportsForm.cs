using System.Diagnostics;
using Afsara.Application.Common.Interfaces;
using Afsara.Application.Orders;
using Afsara.Application.Reports;
using Afsara.Application.Settings;
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
    private SalesReport? _report;

    public ReportsForm(IReportService reports, IOrderService orders, IReceiptPrinter printer, IReportExporter exporter, ISettingsService settings)
    {
        _reports = reports;
        _orders = orders;
        _printer = printer;
        _exporter = exporter;
        _settings = settings;

        Theme.ApplyDialog(this, "Sales Reports", new Size(1320, 760));
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimumSize = new Size(1000, 600);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
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
        filter.Controls.Add(FilterButton("Show Report", Theme.Primary, () => _ = LoadReportAsync()));
        filter.Controls.Add(FilterButton("Print Summary", Theme.Purple, () => _ = PrintSummaryAsync()));
        filter.Controls.Add(FilterButton("Export Excel", Theme.Success, () => _ = ExportAsync(pdf: false)));
        filter.Controls.Add(FilterButton("Export PDF", Theme.Danger, () => _ = ExportAsync(pdf: true)));
        root.Controls.Add(filter, 0, 0);

        root.Controls.Add(_cards, 0, 1);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.Font(10.5f, FontStyle.Bold), Padding = new Point(16, 6) };
        var ordersPage = new TabPage("Orders") { BackColor = Theme.Surface };
        Theme.StyleGrid(_ordersGrid);
        _ordersGrid.Font = Theme.Font(10f);
        ordersPage.Controls.Add(_ordersGrid);
        var ordersBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(4), FlowDirection = FlowDirection.RightToLeft };
        ordersBar.Controls.Add(FilterButton("Reprint Invoice", Theme.Success, () => _ = ReprintAsync()));
        ordersPage.Controls.Add(ordersBar);
        _ordersGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) _ = ReprintAsync(); };
        tabs.TabPages.Add(ordersPage);

        var itemsPage = new TabPage("Item-wise Sales") { BackColor = Theme.Surface };
        Theme.StyleGrid(_itemsGrid);
        _itemsGrid.Font = Theme.Font(10f);
        itemsPage.Controls.Add(_itemsGrid);
        tabs.TabPages.Add(itemsPage);
        root.Controls.Add(tabs, 0, 2);

        _from.Value = DateTime.Today;
        _to.Value = DateTime.Today;
        Load += async (_, _) => await LoadReportAsync();
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

    private async Task LoadReportAsync()
    {
        if (_from.Value.Date > _to.Value.Date)
        {
            Ui.Warn(this, "'From' date cannot be after 'To' date.");
            return;
        }

        await this.RunAsync(async () =>
        {
            _report = await _reports.GetSalesReportAsync(_from.Value, _to.Value);
            RenderCards(_report);

            _ordersGrid.DataSource = _report.Orders.Select(o => new
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

            _itemsGrid.DataSource = _report.Items.Select((i, index) => new
            {
                No = index + 1,
                i.Item,
                Quantity = i.Quantity,
                Amount = Ui.Money(i.Amount)
            }).ToList();
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

    private static Control Card(string title, string value, Color accent, bool highlight = false)
    {
        var card = new Panel
        {
            Size = new Size(170, 84),
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

    private async Task ExportAsync(bool pdf)
    {
        if (_report is null) return;
        var report = _report;
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
