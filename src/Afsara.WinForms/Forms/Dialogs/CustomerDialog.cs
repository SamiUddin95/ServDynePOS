using Afsara.Application.Customers;
using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Dialogs;

public class CustomerDialog : Form
{
    private readonly ICustomerService _customers;
    private readonly TextBox _phone;
    private readonly TextBox _name;
    private readonly TextBox _address;
    private readonly NumericUpDown _deliveryCharge;

    public string CustomerPhone => _phone.Text.Trim();
    public string CustomerName => _name.Text.Trim();
    public string DeliveryAddress => _address.Text.Trim();
    public decimal DeliveryCharge => _deliveryCharge.Value;

    public CustomerDialog(ICustomerService customers, bool isDelivery, string? phone, string? name, string? address, decimal deliveryCharge)
    {
        _customers = customers;
        Theme.ApplyDialog(this, isDelivery ? "Delivery Customer" : "Customer Details", new Size(480, isDelivery ? 420 : 250));

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        Controls.Add(layout);

        _phone = Theme.TextBox("03XX-XXXXXXX");
        _phone.Text = phone ?? string.Empty;
        var search = Theme.Button("Find", Theme.Primary, 9.5f);
        search.Click += async (_, _) => await LookupAsync();
        _phone.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            await LookupAsync();
        };

        _name = Theme.TextBox("Customer name");
        _name.Text = name ?? string.Empty;
        _address = Theme.TextBox("House #, Street, Area");
        _address.Text = address ?? string.Empty;
        _address.Multiline = true;
        _deliveryCharge = Theme.Numeric();
        _deliveryCharge.Value = deliveryCharge;

        var row = 0;
        void AddRow(string label, Control control, float height, Control? extra = null)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(Theme.Label(label, 10f, FontStyle.Bold), 0, row);
            layout.Controls.Add(control, 1, row);
            if (extra is null) layout.SetColumnSpan(control, 2);
            else layout.Controls.Add(extra, 2, row);
            row++;
        }

        AddRow("Phone *", _phone, 44, search);
        AddRow("Name", _name, 44);
        if (isDelivery)
        {
            AddRow("Address *", _address, 100);
            AddRow("Delivery Charges", _deliveryCharge, 44);
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        row++;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        var cancel = Theme.OutlineButton("Cancel");
        cancel.DialogResult = DialogResult.Cancel;
        var ok = Theme.Button("SAVE", Theme.Success);
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_phone.Text))
            {
                Ui.Warn(this, "Customer phone is required.");
                return;
            }
            if (isDelivery && string.IsNullOrWhiteSpace(_address.Text))
            {
                Ui.Warn(this, "Delivery address is required.");
                return;
            }
            DialogResult = DialogResult.OK;
        };
        layout.Controls.Add(cancel, 0, row);
        layout.Controls.Add(ok, 1, row);
        layout.SetColumnSpan(ok, 2);

        CancelButton = cancel;
        Shown += (_, _) => _phone.Focus();
    }

    private async Task LookupAsync()
    {
        await this.RunAsync(async () =>
        {
            var customer = await _customers.FindByPhoneAsync(_phone.Text);
            if (customer is null)
            {
                _name.Focus();
                return;
            }
            _name.Text = customer.Name;
            if (!string.IsNullOrWhiteSpace(customer.Address)) _address.Text = customer.Address;
        });
    }
}
