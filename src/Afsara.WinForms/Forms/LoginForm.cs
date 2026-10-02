using Afsara.Application.Auth;
using Afsara.Application.Licensing;
using Afsara.WinForms.Common;
using Afsara.WinForms.Forms.Dialogs;

namespace Afsara.WinForms.Forms;

public class LoginForm : Form
{
    private readonly IAuthService _auth;
    private readonly TextBox _username;
    private readonly TextBox _password;
    private readonly Button _login;
    private readonly ILicenseService _license;
    private readonly Label _licenseLabel = Theme.Label(string.Empty, 9f, FontStyle.Bold);

    public LoginForm(IAuthService auth, ILicenseService license)
    {
        _auth = auth;
        _license = license;

        Text = $"{Brand.Product} - Sign In";
        Brand.Apply(this);
        Font = Theme.Font();
        ClientSize = new Size(860, 550);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Theme.Surface;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var brand = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = Padding.Empty, Padding = new Padding(28, 12, 28, 16) };
        brand.Controls.Add(new Label
        {
            Text = "Restaurant Point of Sale\n\n- Dine In / Take Away / Delivery\n- Kitchen Order Tickets (KOT)\n- Thermal Invoices & Guest Bills\n- Sales Reports & Day End",
            ForeColor = Brand.Navy,
            Font = Theme.Font(10.5f),
            Dock = DockStyle.Fill,
            Padding = new Padding(40, 4, 0, 0)
        });
        if (Brand.Logo is { } logo)
        {
            brand.Controls.Add(new PictureBox
            {
                Image = logo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Top,
                Height = 290
            });
        }
        else
        {
            brand.Controls.Add(new Label
            {
                Text = Brand.Product.ToUpperInvariant(),
                ForeColor = Brand.Navy,
                Font = Theme.Font(26f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 70
            });
        }
        brand.Controls.Add(new Label
        {
            Text = $"Version {typeof(LoginForm).Assembly.GetName().Version?.ToString(3)}",
            ForeColor = Theme.Muted,
            Font = Theme.Font(9f),
            Dock = DockStyle.Bottom,
            Height = 24
        });
        layout.Controls.Add(brand, 0, 0);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(56, 70, 56, 40),
            ColumnCount = 1,
            RowCount = 8
        };
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(form, 1, 0);

        form.Controls.Add(Theme.Label("Welcome back", 20f, FontStyle.Bold), 0, 0);
        form.Controls.Add(Theme.Label("Sign in to start your shift", 10.5f, FontStyle.Regular, Theme.Muted), 0, 1);
        form.Controls.Add(Theme.Label("Username", 10f, FontStyle.Bold), 0, 2);
        _username = Theme.TextBox("Enter username");
        _username.Font = Theme.Font(13f);
        form.Controls.Add(_username, 0, 3);
        form.Controls.Add(Theme.Label("Password", 10f, FontStyle.Bold), 0, 4);
        _password = Theme.TextBox("Enter password");
        _password.Font = Theme.Font(13f);
        _password.UseSystemPasswordChar = true;
        form.Controls.Add(_password, 0, 5);

        _login = Theme.Button("SIGN IN", Theme.Primary, 12f);
        _login.Margin = new Padding(3, 18, 3, 6);
        _login.Click += async (_, _) => await LoginAsync();
        form.Controls.Add(_login, 0, 6);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
        footer.Controls.Add(Theme.Label("Default login: admin / admin123", 9f, FontStyle.Italic, Theme.Muted));
        var updateLicense = Theme.OutlineButton("Update License", 9.5f);
        updateLicense.Dock = DockStyle.None;
        updateLicense.Size = new Size(150, 34);
        updateLicense.Margin = new Padding(3, 12, 3, 3);
        updateLicense.Click += async (_, _) => await UpdateLicenseAsync();
        footer.Controls.Add(updateLicense);
        footer.Controls.Add(_licenseLabel);
        form.Controls.Add(footer, 0, 7);

        AcceptButton = _login;
        Shown += (_, _) => _username.Focus();
        Load += async (_, _) => await this.RunAsync(RefreshLicenseAsync);
    }

    private async Task UpdateLicenseAsync()
    {
        using (var dialog = new LicenseForm(_license)) dialog.ShowDialog(this);
        await this.RunAsync(RefreshLicenseAsync);
    }

    private async Task RefreshLicenseAsync()
    {
        var status = await _license.GetStatusAsync();
        _licenseLabel.Text = status.IsValid
            ? $"License valid until {status.ExpiresOn:dd MMM yyyy}" + (status.IsExpiringSoon ? $" ({status.RemainingText})" : string.Empty)
            : "License expired - renewal required";
        _licenseLabel.ForeColor = !status.IsValid ? Theme.Danger : status.IsExpiringSoon ? Theme.Warning : Theme.Muted;
    }

    private async Task LoginAsync()
    {
        _login.Enabled = false;
        await this.RunAsync(async () =>
        {
            await _auth.LoginAsync(_username.Text, _password.Text);
            DialogResult = DialogResult.OK;
            Close();
        });
        if (!IsDisposed)
        {
            _login.Enabled = true;
            _password.SelectAll();
            _password.Focus();
        }
    }
}
