using Afsara.Application.Licensing;
using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Dialogs;

public class LicenseForm : Form
{
    private readonly ILicenseService _license;
    private readonly TextBox _key;

    public LicenseForm(ILicenseService license)
    {
        _license = license;

        Theme.ApplyDialog(this, $"{Brand.Product} - License", new Size(580, 460));
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 16), ColumnCount = 2, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(layout);

        var title = Theme.Label("License Activation", 16f, FontStyle.Bold);
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 2);

        var message = new Label
        {
            Text = "Checking license...",
            Dock = DockStyle.Fill,
            Font = Theme.Font(10.5f, FontStyle.Bold),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(message, 0, 1);
        layout.SetColumnSpan(message, 2);

        var machineCaption = Theme.Label("Machine ID (share this when requesting a license)", 10f, FontStyle.Bold);
        layout.Controls.Add(machineCaption, 0, 2);
        layout.SetColumnSpan(machineCaption, 2);

        var machineId = Theme.TextBox();
        machineId.Text = license.MachineId;
        machineId.ReadOnly = true;
        machineId.Font = Theme.Font(12f, FontStyle.Bold);
        layout.Controls.Add(machineId, 0, 3);
        var copy = Theme.OutlineButton("Copy");
        copy.Click += (_, _) =>
        {
            Clipboard.SetText(license.MachineId);
            Ui.Info(this, "Machine ID copied to clipboard.");
        };
        layout.Controls.Add(copy, 1, 3);

        var keyCaption = Theme.Label("License Key", 10f, FontStyle.Bold);
        layout.Controls.Add(keyCaption, 0, 4);
        layout.SetColumnSpan(keyCaption, 2);

        _key = Theme.TextBox("Paste the license key provided by ServDyne");
        _key.Multiline = true;
        _key.ScrollBars = ScrollBars.Vertical;
        _key.Font = Theme.Font(9.5f);
        layout.Controls.Add(_key, 0, 5);
        layout.SetColumnSpan(_key, 2);

        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 8, 0, 0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var cancel = Theme.OutlineButton("Exit");
        cancel.DialogResult = DialogResult.Cancel;
        var activate = Theme.Button("ACTIVATE", Theme.Success, 11f);
        activate.Click += async (_, _) => await ActivateAsync();
        buttons.Controls.Add(cancel, 0, 0);
        buttons.Controls.Add(activate, 1, 0);
        layout.Controls.Add(buttons, 0, 6);
        layout.SetColumnSpan(buttons, 2);

        CancelButton = cancel;
        Shown += (_, _) => _key.Focus();
        Load += async (_, _) => await this.RunAsync(async () =>
        {
            var status = await _license.GetStatusAsync();
            title.Text = status.IsValid ? "Update License" : "License Activation";
            message.Text = status.Message;
            message.ForeColor = !status.IsValid ? Theme.Danger : status.IsExpiringSoon ? Theme.Warning : Theme.Success;
            cancel.Text = status.IsValid ? "Close" : "Exit";
        });
    }

    private Task ActivateAsync() => this.RunAsync(async () =>
    {
        var status = await _license.ActivateAsync(_key.Text);
        Ui.Info(this, $"License activated successfully.\n\nLicensed to: {status.Licensee}\nValid until: {status.ExpiresOn:dd MMM yyyy}");
        DialogResult = DialogResult.OK;
        Close();
    });
}
