using Afsara.Application.Common.Interfaces;
using Afsara.Application.Printing;
using Afsara.Application.Settings;
using Afsara.Domain.Entities;
using Afsara.Printing;
using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Admin;

public class SettingsForm : Form
{
    private const string DefaultPrinter = "(Windows default printer)";

    private readonly ISettingsService _settings;
    private readonly IReceiptPrinter _printer;

    private readonly TextBox _name = Theme.TextBox();
    private readonly TextBox _address = Theme.TextBox();
    private readonly TextBox _phone = Theme.TextBox();
    private readonly TextBox _taxNumber = Theme.TextBox();
    private readonly TextBox _currency = Theme.TextBox();
    private readonly NumericUpDown _cashTax = Theme.Numeric(100, 2);
    private readonly NumericUpDown _cardTax = Theme.Numeric(100, 2);
    private readonly NumericUpDown _service = Theme.Numeric(100, 2);
    private readonly NumericUpDown _delivery = Theme.Numeric(100_000);
    private readonly TextBox _footer = Theme.TextBox();
    private readonly TextBox _logo = Theme.TextBox("Optional - PNG/JPG shown on invoice");
    private readonly ComboBox _kotPrinter = Theme.Combo();
    private readonly ComboBox _invoicePrinter = Theme.Combo();
    private readonly NumericUpDown _copies = Theme.Numeric(5);
    private readonly CheckBox _preview = new() { Text = "Show print preview before printing", Font = Theme.Font(10.5f), AutoSize = true };

    public SettingsForm(ISettingsService settings, IReceiptPrinter printer)
    {
        _settings = settings;
        _printer = printer;
        Theme.ApplyDialog(this, "Settings", new Size(900, 640));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(root);

        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.Controls.Add(columns, 0, 0);

        var business = Section("Restaurant / Invoice Header");
        AddField(business, "Restaurant Name", _name);
        AddField(business, "Address", _address);
        AddField(business, "Phone", _phone);
        AddField(business, "NTN / STRN", _taxNumber);
        AddField(business, "Invoice Footer", _footer);
        var logoRow = new TableLayoutPanel { ColumnCount = 2, Margin = Padding.Empty };
        logoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        logoRow.Controls.Add(_logo, 0, 0);
        var browse = Theme.OutlineButton("Browse", 9f);
        browse.Click += (_, _) => BrowseLogo();
        logoRow.Controls.Add(browse, 1, 0);
        AddField(business, "Logo", logoRow);
        columns.Controls.Add(Wrap(business), 0, 0);

        var billing = Section("Taxes, Charges & Printing");
        AddField(billing, "Currency Symbol", _currency);
        AddField(billing, "Sales Tax % (Cash)", _cashTax);
        AddField(billing, "Sales Tax % (Card / Online)", _cardTax);
        AddField(billing, "Service Charges % (Dine In)", _service);
        AddField(billing, "Default Delivery Charges", _delivery);
        AddField(billing, "KOT Printer (Kitchen)", _kotPrinter);
        AddField(billing, "Invoice Printer (Counter)", _invoicePrinter);
        _copies.Minimum = 1;
        AddField(billing, "Invoice Copies", _copies);
        AddField(billing, string.Empty, _preview);
        columns.Controls.Add(Wrap(billing), 1, 0);

        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        var test = Theme.Button("Test Print", Theme.Purple);
        test.Click += async (_, _) => await TestPrintAsync();
        var cancel = Theme.OutlineButton("Close");
        cancel.DialogResult = DialogResult.Cancel;
        var save = Theme.Button("SAVE SETTINGS", Theme.Success);
        save.Click += async (_, _) => await SaveAsync(closeAfter: true);
        buttons.Controls.Add(test, 1, 0);
        buttons.Controls.Add(cancel, 2, 0);
        buttons.Controls.Add(save, 3, 0);
        root.Controls.Add(buttons, 0, 1);

        CancelButton = cancel;
        Load += async (_, _) => await this.RunAsync(LoadAsync);
    }

    private static TableLayoutPanel Section(string title)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(14), BackColor = Theme.Surface };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(Theme.Label(title, 12f, FontStyle.Bold, Theme.Primary));
        return panel;
    }

    private static Control Wrap(Control section)
    {
        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(4) };
        host.Controls.Add(section);
        return host;
    }

    private static void AddField(TableLayoutPanel panel, string label, Control control)
    {
        if (!string.IsNullOrEmpty(label))
            panel.Controls.Add(Theme.Label(label, 9.5f, FontStyle.Bold, Theme.Muted));
        control.Dock = DockStyle.Top;
        if (control is TableLayoutPanel) control.Height = 36;
        panel.Controls.Add(control);
    }

    private async Task LoadAsync()
    {
        var s = await _settings.GetAsync();
        _name.Text = s.RestaurantName;
        _address.Text = s.Address;
        _phone.Text = s.Phone;
        _taxNumber.Text = s.TaxNumber ?? string.Empty;
        _currency.Text = s.CurrencySymbol;
        _cashTax.Value = s.CashTaxPercent;
        _cardTax.Value = s.CardTaxPercent;
        _service.Value = s.ServiceChargePercent;
        _delivery.Value = s.DefaultDeliveryCharge;
        _footer.Text = s.InvoiceFooter;
        _logo.Text = s.LogoPath ?? string.Empty;
        _copies.Value = Math.Clamp(s.InvoiceCopies, 1, 5);
        _preview.Checked = s.ShowPrintPreview;

        var printers = ThermalReceiptPrinter.InstalledPrinters();
        foreach (var combo in new[] { _kotPrinter, _invoicePrinter })
        {
            combo.Items.Clear();
            combo.Items.Add(DefaultPrinter);
            foreach (var p in printers) combo.Items.Add(p);
        }
        SelectPrinter(_kotPrinter, s.KotPrinterName);
        SelectPrinter(_invoicePrinter, s.InvoicePrinterName);
    }

    private static void SelectPrinter(ComboBox combo, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name) && !combo.Items.Contains(name)) combo.Items.Add(name);
        combo.SelectedItem = string.IsNullOrWhiteSpace(name) ? DefaultPrinter : name;
    }

    private static string? PrinterValue(ComboBox combo) =>
        combo.SelectedItem is string s && s != DefaultPrinter ? s : null;

    private void BrowseLogo()
    {
        using var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp", Title = "Select invoice logo" };
        if (dialog.ShowDialog(this) == DialogResult.OK) _logo.Text = dialog.FileName;
    }

    private async Task SaveAsync(bool closeAfter)
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            Ui.Warn(this, "Restaurant name is required.");
            return;
        }

        await this.RunAsync(async () =>
        {
            await _settings.SaveAsync(new RestaurantSetting
            {
                RestaurantName = _name.Text,
                Address = _address.Text,
                Phone = _phone.Text,
                TaxNumber = _taxNumber.Text,
                CurrencySymbol = string.IsNullOrWhiteSpace(_currency.Text) ? "Rs." : _currency.Text,
                CashTaxPercent = _cashTax.Value,
                CardTaxPercent = _cardTax.Value,
                ServiceChargePercent = _service.Value,
                DefaultDeliveryCharge = _delivery.Value,
                InvoiceFooter = _footer.Text,
                LogoPath = string.IsNullOrWhiteSpace(_logo.Text) ? null : _logo.Text.Trim(),
                KotPrinterName = PrinterValue(_kotPrinter),
                InvoicePrinterName = PrinterValue(_invoicePrinter),
                InvoiceCopies = (int)_copies.Value,
                ShowPrintPreview = _preview.Checked
            });

            if (closeAfter)
            {
                Ui.Info(this, "Settings saved successfully.");
                DialogResult = DialogResult.OK;
            }
        });
    }

    private async Task TestPrintAsync()
    {
        await SaveAsync(closeAfter: false);
        await this.RunAsync(async () =>
        {
            var s = await _settings.GetAsync();
            var now = DateTime.Now;
            await _printer.PrintKotAsync(new KotTicket
            {
                RestaurantName = s.RestaurantName,
                KotNumber = 1,
                OrderNo = "0000",
                OrderType = "Dine In",
                TableName = "T-1",
                WaiterName = "Test Waiter",
                Guests = 2,
                CreatedBy = "Test",
                PrintedAt = now,
                Lines =
                {
                    new KotLine { Name = "Chicken Tikka", Section = "BBQ", Quantity = 2, Notes = "Extra spicy" },
                    new KotLine { Name = "Garlic Naan", Section = "TANDOOR", Quantity = 4 }
                }
            });

            var sub = 2 * 650m + 4 * 120m;
            var tax = Math.Round(sub * s.CashTaxPercent / 100m, 0, MidpointRounding.AwayFromZero);
            await _printer.PrintInvoiceAsync(new InvoiceDocument
            {
                RestaurantName = s.RestaurantName,
                Address = s.Address,
                Phone = s.Phone,
                TaxNumber = s.TaxNumber,
                LogoPath = s.LogoPath,
                Footer = s.InvoiceFooter,
                Currency = s.CurrencySymbol,
                IsPaid = true,
                IsDuplicate = true,
                InvoiceNo = "INV-000000",
                OrderNo = "0000",
                OrderType = "Dine In",
                TableName = "T-1",
                WaiterName = "Test Waiter",
                Cashier = "Test",
                Guests = 2,
                Date = now,
                Lines =
                {
                    new InvoiceLine { Name = "Chicken Tikka", Quantity = 2, Rate = 650 },
                    new InvoiceLine { Name = "Garlic Naan", Quantity = 4, Rate = 120 }
                },
                SubTotal = sub,
                TaxPercent = s.CashTaxPercent,
                Tax = tax,
                GrandTotal = sub + tax,
                PaymentMethod = "Cash",
                Paid = sub + tax,
                Change = 0
            });
        });
    }
}
