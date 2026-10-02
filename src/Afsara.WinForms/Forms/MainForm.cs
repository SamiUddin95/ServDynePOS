using Afsara.Application.Auth;
using Afsara.Application.Catalog;
using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Application.Customers;
using Afsara.Application.Licensing;
using Afsara.Application.Orders;
using Afsara.Application.Settings;
using Afsara.Domain.Entities;
using Afsara.Domain.Enums;
using Afsara.WinForms.Common;
using Afsara.WinForms.Forms.Admin;
using Afsara.WinForms.Forms.Dialogs;

namespace Afsara.WinForms.Forms;

public class MainForm : Form
{
    private readonly IOrderService _orders;
    private readonly ICatalogService _catalog;
    private readonly ISettingsService _settingsService;
    private readonly ICustomerService _customers;
    private readonly IReceiptPrinter _printer;
    private readonly IUserSession _session;
    private readonly IAuthService _auth;
    private readonly ILicenseService _license;
    private readonly FormFactory _forms;

    private RestaurantSetting _settings = new();
    private List<Category> _categories = new();
    private List<MenuItem> _items = new();
    private readonly Dictionary<int, Color> _categoryColors = new();
    private int? _categoryId;
    private OrderDraft _draft = new();
    private bool _binding;

    private readonly Label _clock = new();
    private readonly Label _restaurantLabel = new();
    private readonly Label _userLabel = new();
    private readonly Label _status = new();
    private readonly Label _licenseLabel = new();
    private DateTime _licenseCheckedOn;
    private readonly Label _orderTitle = new();
    private readonly Dictionary<OrderType, Button> _typeButtons = new();
    private Button _tableButton = null!;
    private Button _customerButton = null!;
    private readonly ComboBox _waiter = Theme.Combo();
    private readonly NumericUpDown _guests = Theme.Numeric(100);
    private readonly DataGridView _cart = new();
    private readonly Label _subTotal = TotalValue();
    private readonly Label _discountCaption = TotalCaption("Discount");
    private readonly Label _discount = TotalValue();
    private readonly Label _serviceCaption = TotalCaption("Service Charges");
    private readonly Label _service = TotalValue();
    private readonly Label _taxCaption = TotalCaption("Sales Tax");
    private readonly Label _tax = TotalValue();
    private readonly Label _delivery = TotalValue();
    private readonly Label _grandTotal = new();
    private readonly TextBox _search = Theme.TextBox("Search item by name or code  (F2)");
    private readonly FlowLayoutPanel _categoryPanel = new();
    private readonly FlowLayoutPanel _itemPanel = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };

    public bool LogoutRequested { get; private set; }

    public MainForm(IOrderService orders, ICatalogService catalog, ISettingsService settingsService,
        ICustomerService customers, IReceiptPrinter printer, IUserSession session, IAuthService auth,
        ILicenseService license, FormFactory forms)
    {
        _orders = orders;
        _catalog = catalog;
        _settingsService = settingsService;
        _customers = customers;
        _printer = printer;
        _session = session;
        _auth = auth;
        _license = license;
        _forms = forms;

        Text = Brand.Product;
        Brand.Apply(this);
        Font = Theme.Font();
        BackColor = Theme.Background;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 720);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(6), Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        body.Controls.Add(BuildOrderPanel(), 0, 0);
        body.Controls.Add(BuildMenuPanel(), 1, 0);
        root.Controls.Add(body, 0, 1);

        _status.Dock = DockStyle.Fill;
        _status.BackColor = Theme.Surface;
        _status.ForeColor = Theme.Muted;
        _status.Padding = new Padding(10, 0, 0, 0);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Margin = Padding.Empty;
        _status.Text = "Ready.  F1 New  |  F2 Search  |  F4 Hold  |  F5 KOT  |  F6 Running Orders  |  F7 Table  |  F8 Bill  |  F9 Pay";
        root.Controls.Add(BuildStatusBar(), 0, 2);

        _timer.Tick += (_, _) =>
        {
            _clock.Text = DateTime.Now.ToString("ddd, dd MMM yyyy   hh:mm:ss tt");
            if (DateTime.Today != _licenseCheckedOn) _ = this.RunAsync(async () => await RefreshLicenseAsync());
        };
        _clock.Text = DateTime.Now.ToString("ddd, dd MMM yyyy   hh:mm:ss tt");
        _timer.Start();

        Load += async (_, _) => await this.RunAsync(LoadDataAsync);
        Shown += async (_, _) => await this.RunAsync(WarnLicenseExpiryAsync);
        FormClosing += OnFormClosing;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Theme.Header, Margin = Padding.Empty, Padding = new Padding(12, 0, 8, 0) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var brand = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        brand.Controls.Add(new Label { Text = "SERV", ForeColor = Color.White, Font = Theme.Font(17f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 14, 0, 0) });
        brand.Controls.Add(new Label { Text = "DYNE", ForeColor = Brand.Orange, Font = Theme.Font(17f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 14, 12, 0) });
        _restaurantLabel.ForeColor = Color.FromArgb(148, 163, 184);
        _restaurantLabel.Font = Theme.Font(10.5f);
        _restaurantLabel.AutoSize = true;
        _restaurantLabel.Margin = new Padding(0, 21, 0, 0);
        brand.Controls.Add(_restaurantLabel);
        header.Controls.Add(brand, 0, 0);

        var info = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        _clock.ForeColor = Color.White;
        _clock.Font = Theme.Font(10.5f, FontStyle.Bold);
        _clock.AutoSize = true;
        _clock.Margin = new Padding(0, 20, 20, 0);
        info.Controls.Add(_clock);
        var user = _session.RequireUser();
        _userLabel.Text = $"{user.FullName} ({user.Role})";
        _userLabel.ForeColor = Color.FromArgb(125, 211, 252);
        _userLabel.Font = Theme.Font(10.5f, FontStyle.Underline);
        _userLabel.AutoSize = true;
        _userLabel.Cursor = Cursors.Hand;
        _userLabel.Margin = new Padding(0, 20, 0, 0);
        _userLabel.Click += async (_, _) => await ChangePasswordAsync();
        info.Controls.Add(_userLabel);
        header.Controls.Add(info, 1, 0);

        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 10, 0, 0) };
        actions.Controls.Add(HeaderButton("Running Orders", Theme.Warning, async () => await OpenRunningOrdersAsync()));
        if (user.CanManage)
        {
            actions.Controls.Add(HeaderButton("Reports", Theme.Teal, () => { using var f = _forms.Create<ReportsForm>(); f.ShowDialog(this); return Task.CompletedTask; }));
            actions.Controls.Add(HeaderButton("Setup", Theme.Purple, async () => { using (var f = _forms.Create<MasterDataForm>()) f.ShowDialog(this); await this.RunAsync(LoadDataAsync); }));
            actions.Controls.Add(HeaderButton("Settings", Theme.Gray, async () => { using (var f = _forms.Create<SettingsForm>()) f.ShowDialog(this); await this.RunAsync(LoadDataAsync); }));
        }
        actions.Controls.Add(HeaderButton("Logout", Theme.Danger, () => { Logout(); return Task.CompletedTask; }));
        header.Controls.Add(actions, 2, 0);
        return header;
    }

    private Control BuildStatusBar()
    {
        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Surface, Margin = Padding.Empty };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.Controls.Add(_status, 0, 0);

        _licenseLabel.AutoSize = true;
        _licenseLabel.Anchor = AnchorStyles.Right;
        _licenseLabel.Margin = new Padding(0, 0, 12, 0);
        if (_session.RequireUser().CanManage)
        {
            _licenseLabel.Cursor = Cursors.Hand;
            _licenseLabel.Click += async (_, _) => await UpdateLicenseAsync();
        }
        bar.Controls.Add(_licenseLabel, 1, 0);
        return bar;
    }

    private async Task<LicenseStatus> RefreshLicenseAsync()
    {
        _licenseCheckedOn = DateTime.Today;
        var status = await _license.GetStatusAsync();
        _licenseLabel.Text = status.IsValid
            ? $"License valid until {status.ExpiresOn:dd MMM yyyy}" + (status.IsExpiringSoon ? $"  ({status.RemainingText})" : string.Empty)
            : "License expired - renewal required";
        _licenseLabel.ForeColor = !status.IsValid ? Theme.Danger : status.IsExpiringSoon ? Theme.Warning : Theme.Muted;
        _licenseLabel.Font = Theme.Font(9.5f, status.IsExpiringSoon || !status.IsValid ? FontStyle.Bold : FontStyle.Regular);
        return status;
    }

    private async Task WarnLicenseExpiryAsync()
    {
        var status = await RefreshLicenseAsync();
        if (!status.IsExpiringSoon) return;

        var message = $"Your license will expire on {status.ExpiresOn:dd MMM yyyy} ({status.RemainingText}).\n\n" +
                      $"Please clear your pending dues and contact {Brand.Company} to renew your license and avoid any interruption of service.";
        if (!_session.RequireUser().CanManage)
        {
            Ui.Warn(this, message);
            return;
        }
        if (MessageBox.Show(this, $"{message}\n\nDo you already have a new license key to enter now?", $"{Brand.Product} - License Expiring",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            await UpdateLicenseAsync();
    }

    private async Task UpdateLicenseAsync()
    {
        using (var form = _forms.Create<LicenseForm>()) form.ShowDialog(this);
        await this.RunAsync(async () => await RefreshLicenseAsync());
    }

    private static Button HeaderButton(string text, Color color, Func<Task> onClick)
    {
        var button = Theme.Button(text, color, 9.5f);
        button.Dock = DockStyle.None;
        button.Size = new Size(text.Length > 10 ? 136 : 100, 38);
        button.Margin = new Padding(4, 0, 4, 0);
        button.Click += async (_, _) => await onClick();
        return button;
    }

    private Control BuildOrderPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, BackColor = Theme.Surface, Padding = new Padding(8), Margin = new Padding(0, 0, 6, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));

        var types = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        foreach (var (type, index) in new[] { (OrderType.DineIn, 0), (OrderType.TakeAway, 1), (OrderType.Delivery, 2) })
        {
            types.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            var button = Theme.Button(type.ToDisplay().ToUpperInvariant(), Theme.Gray, 12f);
            button.Click += (_, _) => SetOrderType(type);
            types.Controls.Add(button, index, 0);
            _typeButtons[type] = button;
        }
        panel.Controls.Add(types, 0, 0);

        var info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Margin = Padding.Empty };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        _orderTitle.Dock = DockStyle.Fill;
        _orderTitle.Font = Theme.Font(13f, FontStyle.Bold);
        _orderTitle.TextAlign = ContentAlignment.MiddleLeft;
        info.Controls.Add(_orderTitle, 0, 0);
        info.SetColumnSpan(_orderTitle, 2);
        info.Controls.Add(new Label { Text = "Guests", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Muted }, 2, 0);
        _guests.Minimum = 1;
        _guests.ValueChanged += (_, _) => { if (!_binding) _draft.Guests = (int)_guests.Value; };
        info.Controls.Add(_guests, 3, 0);

        _tableButton = Theme.Button("Select Table (F7)", Theme.Primary, 11f);
        _tableButton.Click += async (_, _) => await SelectTableAsync();
        info.Controls.Add(_tableButton, 0, 1);

        _waiter.SelectedIndexChanged += (_, _) =>
        {
            if (!_binding) _draft.WaiterId = (_waiter.SelectedItem as ComboItem)?.Id;
        };
        info.Controls.Add(_waiter, 1, 1);
        info.SetColumnSpan(_waiter, 3);

        _customerButton = Theme.OutlineButton("Customer: Walk-in", 10.5f);
        _customerButton.TextAlign = ContentAlignment.MiddleLeft;
        _customerButton.Click += (_, _) => EditCustomer();
        info.Controls.Add(_customerButton, 0, 2);
        info.SetColumnSpan(_customerButton, 4);
        panel.Controls.Add(info, 0, 1);

        Theme.StyleGrid(_cart);
        _cart.AutoGenerateColumns = false;
        _cart.Columns.Add(new DataGridViewTextBoxColumn { Name = "No", HeaderText = "#", FillWeight = 8 });
        _cart.Columns.Add(new DataGridViewTextBoxColumn { Name = "Item", HeaderText = "Item", FillWeight = 46 });
        _cart.Columns.Add(new DataGridViewTextBoxColumn { Name = "Qty", HeaderText = "Qty", FillWeight = 10, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = Theme.Font(11f, FontStyle.Bold) } });
        _cart.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rate", HeaderText = "Rate", FillWeight = 14, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
        _cart.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Amount", FillWeight = 15, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = Theme.Font(10.5f, FontStyle.Bold) } });
        _cart.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kot", HeaderText = "KOT", FillWeight = 10, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        foreach (DataGridViewColumn column in _cart.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        _cart.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditNote(); };
        _cart.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) { RemoveLine(); e.Handled = true; }
            else if (e.KeyCode is Keys.Add or Keys.Oemplus) { ChangeQuantity(1); e.Handled = true; }
            else if (e.KeyCode is Keys.Subtract or Keys.OemMinus) { ChangeQuantity(-1); e.Handled = true; }
        };
        panel.Controls.Add(_cart, 0, 2);

        var cartActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, Margin = Padding.Empty };
        var cartButtons = new (string Text, Color Color, Action Action)[]
        {
            ("+", Theme.Success, () => ChangeQuantity(1)),
            ("-", Theme.Warning, () => ChangeQuantity(-1)),
            ("Remove", Theme.Danger, RemoveLine),
            ("Note", Theme.Primary, EditNote),
            ("Discount", Theme.Purple, EditDiscount),
            ("Clear", Theme.Gray, ClearScreen)
        };
        for (var i = 0; i < cartButtons.Length; i++)
        {
            cartActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cartButtons.Length));
            var (text, color, action) = cartButtons[i];
            var button = Theme.Button(text, color, text.Length == 1 ? 16f : 10f);
            button.Click += (_, _) => action();
            cartActions.Controls.Add(button, i, 0);
        }
        panel.Controls.Add(cartActions, 0, 3);

        panel.Controls.Add(BuildTotalsPanel(), 0, 4);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Margin = Padding.Empty };
        for (var i = 0; i < 3; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        AddAction(actions, "NEW (F1)", Theme.Gray, 0, 0, () => { ClearScreen(); return Task.CompletedTask; });
        AddAction(actions, "HOLD (F4)", Theme.Primary, 1, 0, HoldAsync);
        AddAction(actions, "CANCEL ORDER", Theme.Danger, 2, 0, CancelOrderAsync);
        AddAction(actions, "KOT (F5)", Theme.Warning, 0, 1, SendKotAsync);
        AddAction(actions, "BILL (F8)", Theme.Purple, 1, 1, PrintBillAsync);
        AddAction(actions, "PAY (F9)", Theme.Success, 2, 1, PayAsync);
        panel.Controls.Add(actions, 0, 5);

        return panel;
    }

    private static void AddAction(TableLayoutPanel panel, string text, Color color, int column, int row, Func<Task> action)
    {
        var button = Theme.Button(text, color, 12.5f);
        button.Click += async (_, _) => await action();
        panel.Controls.Add(button, column, row);
    }

    private Control BuildTotalsPanel()
    {
        var totals = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 4, 0, 4) };
        totals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        totals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        var row = 0;
        void Add(Control caption, Control value, float height)
        {
            totals.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            totals.Controls.Add(caption, 0, row);
            totals.Controls.Add(value, 1, row);
            row++;
        }

        Add(TotalCaption("Sub Total"), _subTotal, 22);
        Add(_discountCaption, _discount, 22);
        Add(_serviceCaption, _service, 22);
        Add(_taxCaption, _tax, 22);
        Add(TotalCaption("Delivery Charges"), _delivery, 22);

        _grandTotal.Dock = DockStyle.Fill;
        _grandTotal.TextAlign = ContentAlignment.MiddleRight;
        _grandTotal.Font = Theme.Font(20f, FontStyle.Bold);
        _grandTotal.ForeColor = Theme.Success;
        Add(new Label { Text = "GRAND TOTAL", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Theme.Font(14f, FontStyle.Bold) }, _grandTotal, 44);
        return totals;
    }

    private static Label TotalCaption(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Theme.Muted,
        Font = Theme.Font(10f),
        Margin = Padding.Empty
    };

    private static Label TotalValue() => new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight,
        Font = Theme.Font(10.5f, FontStyle.Bold),
        Margin = Padding.Empty
    };

    private Control BuildMenuPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, BackColor = Theme.Surface, Padding = new Padding(8), Margin = Padding.Empty };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _search.Font = Theme.Font(13f);
        _search.TextChanged += (_, _) => RenderItems();
        _search.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            var matches = FilteredItems().ToList();
            var exact = matches.FirstOrDefault(m => string.Equals(m.Code, _search.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            var pick = exact ?? (matches.Count == 1 ? matches[0] : null);
            if (pick is null) return;
            AddItem(pick);
            _search.Clear();
        };
        panel.Controls.Add(_search, 0, 0);

        _categoryPanel.Dock = DockStyle.Fill;
        _categoryPanel.AutoScroll = true;
        _categoryPanel.Margin = new Padding(0, 4, 0, 4);
        _categoryPanel.BackColor = Theme.Background;
        _categoryPanel.Padding = new Padding(4);
        panel.Controls.Add(_categoryPanel, 0, 1);

        _itemPanel.Dock = DockStyle.Fill;
        _itemPanel.AutoScroll = true;
        _itemPanel.Padding = new Padding(2);
        panel.Controls.Add(_itemPanel, 0, 2);
        return panel;
    }

    private async Task LoadDataAsync()
    {
        _settings = await _settingsService.GetAsync();
        _categories = await _catalog.GetCategoriesAsync(activeOnly: true);
        _items = await _catalog.GetMenuItemsAsync(activeOnly: true);
        var waiters = await _catalog.GetWaitersAsync(activeOnly: true);

        _binding = true;
        _waiter.Items.Clear();
        _waiter.Items.Add(new ComboItem(null, "-- Select Waiter --"));
        foreach (var waiter in waiters) _waiter.Items.Add(new ComboItem(waiter.Id, waiter.Name));
        _binding = false;

        _categoryColors.Clear();
        for (var i = 0; i < _categories.Count; i++)
            _categoryColors[_categories[i].Id] = Theme.TilePalette[i % Theme.TilePalette.Length];

        if (_categoryId.HasValue && _categories.All(c => c.Id != _categoryId)) _categoryId = null;

        _restaurantLabel.Text = _settings.RestaurantName;
        Text = $"{Brand.Product} - {_settings.RestaurantName}";
        RenderCategories();
        RenderItems();
        RefreshOrder();
    }

    private void RenderCategories()
    {
        _categoryPanel.SuspendLayout();
        var old = _categoryPanel.Controls.Cast<Control>().ToList();
        _categoryPanel.Controls.Clear();
        old.ForEach(c => c.Dispose());

        void Add(int? id, string name)
        {
            var selected = _categoryId == id;
            var button = Theme.Button(name, selected ? Theme.Primary : Theme.Sidebar, 10.5f);
            button.Dock = DockStyle.None;
            button.Size = new Size(132, 46);
            button.Margin = new Padding(3);
            button.Click += (_, _) =>
            {
                _categoryId = id;
                RenderCategories();
                RenderItems();
            };
            _categoryPanel.Controls.Add(button);
        }

        Add(null, "ALL ITEMS");
        foreach (var category in _categories) Add(category.Id, category.Name);
        _categoryPanel.ResumeLayout();
    }

    private IEnumerable<MenuItem> FilteredItems()
    {
        var term = _search.Text.Trim();
        return _items.Where(i =>
            (_categoryId is null || i.CategoryId == _categoryId) &&
            (term.Length == 0 || i.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || i.Code.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    private void RenderItems()
    {
        _itemPanel.SuspendLayout();
        var old = _itemPanel.Controls.Cast<Control>().ToList();
        _itemPanel.Controls.Clear();
        old.ForEach(c => c.Dispose());

        foreach (var item in FilteredItems())
        {
            var color = _categoryColors.GetValueOrDefault(item.CategoryId, Theme.Primary);
            var tile = Theme.Button($"{item.Name}\n\n{_settings.CurrencySymbol} {Ui.Money(item.Price)}", color, 10.5f);
            tile.Dock = DockStyle.None;
            tile.Size = new Size(152, 92);
            tile.Margin = new Padding(4);
            tile.Click += (_, _) => AddItem(item);
            _itemPanel.Controls.Add(tile);
        }
        _itemPanel.ResumeLayout();
    }

    private void RefreshOrder()
    {
        _binding = true;
        foreach (var (type, button) in _typeButtons)
        {
            var color = type == _draft.OrderType ? Theme.Primary : Theme.Gray;
            button.BackColor = color;
            button.FlatAppearance.MouseOverBackColor = Theme.Darken(color, 0.12f);
        }

        _orderTitle.Text = _draft.OrderId is null ? "NEW ORDER" : $"ORDER #{_draft.OrderNo}   (Running)";
        _orderTitle.ForeColor = _draft.OrderId is null ? Theme.Text : Theme.Warning;

        _tableButton.Enabled = _draft.OrderType == OrderType.DineIn;
        _tableButton.Text = _draft.OrderType != OrderType.DineIn ? "No Table"
            : _draft.TableName is null ? "Select Table (F7)" : $"Table: {_draft.TableName}";
        _tableButton.BackColor = _draft.OrderType != OrderType.DineIn ? Theme.Border
            : _draft.TableName is null ? Theme.Primary : Theme.Success;

        var hasCustomer = !string.IsNullOrWhiteSpace(_draft.CustomerPhone) || !string.IsNullOrWhiteSpace(_draft.CustomerName);
        _customerButton.Text = hasCustomer
            ? $"Customer: {_draft.CustomerName} {_draft.CustomerPhone}".Trim() + (string.IsNullOrWhiteSpace(_draft.DeliveryAddress) ? string.Empty : $"  |  {_draft.DeliveryAddress}")
            : _draft.OrderType == OrderType.Delivery ? "Add Delivery Customer (required)" : "Customer: Walk-in  (click to add)";
        _customerButton.ForeColor = !hasCustomer && _draft.OrderType == OrderType.Delivery ? Theme.Danger : Theme.Text;

        _waiter.SelectedIndex = -1;
        for (var i = 0; i < _waiter.Items.Count; i++)
        {
            if (_waiter.Items[i] is ComboItem item && item.Id == _draft.WaiterId)
            {
                _waiter.SelectedIndex = i;
                break;
            }
        }
        if (_waiter.SelectedIndex < 0 && _waiter.Items.Count > 0) _waiter.SelectedIndex = 0;

        _guests.Value = Math.Clamp(_draft.Guests, 1, 100);
        _binding = false;

        RenderCart();
    }

    private void RenderCart(int? selectIndex = null)
    {
        var current = selectIndex ?? _cart.CurrentRow?.Index ?? -1;
        _cart.Rows.Clear();
        for (var i = 0; i < _draft.Lines.Count; i++)
        {
            var line = _draft.Lines[i];
            var name = string.IsNullOrWhiteSpace(line.Notes) ? line.Name : $"{line.Name}  ({line.Notes})";
            var kot = line.PrintedQuantity == 0 ? "New"
                : line.Quantity > line.PrintedQuantity ? $"+{line.Quantity - line.PrintedQuantity}"
                : line.Quantity < line.PrintedQuantity ? $"-{line.PrintedQuantity - line.Quantity}"
                : "Sent";
            var index = _cart.Rows.Add(i + 1, name, line.Quantity, Ui.Money(line.UnitPrice), Ui.Money(line.LineTotal), kot);
            _cart.Rows[index].Cells["Kot"].Style.ForeColor = kot == "Sent" ? Theme.Success : Theme.Warning;
        }

        if (_cart.Rows.Count > 0)
        {
            var target = Math.Clamp(current < 0 ? _cart.Rows.Count - 1 : current, 0, _cart.Rows.Count - 1);
            _cart.CurrentCell = _cart.Rows[target].Cells["Item"];
        }

        RenderTotals();
    }

    private void RenderTotals()
    {
        var totals = OrderCalculator.Calculate(_draft, _settings.CashTaxPercent, _settings.ServiceChargePercent);
        _subTotal.Text = Ui.Money(totals.SubTotal);
        _discountCaption.Text = totals.DiscountPercent > 0 ? $"Discount ({totals.DiscountPercent:0.##}%)" : "Discount";
        _discount.Text = totals.DiscountAmount > 0 ? $"- {Ui.Money(totals.DiscountAmount)}" : "0";
        _serviceCaption.Text = $"Service Charges ({totals.ServiceChargePercent:0.##}%)";
        _service.Text = Ui.Money(totals.ServiceCharge);
        _taxCaption.Text = $"Sales Tax ({totals.TaxPercent:0.##}% cash)";
        _tax.Text = Ui.Money(totals.TaxAmount);
        _delivery.Text = Ui.Money(totals.DeliveryCharge);
        _grandTotal.Text = $"{_settings.CurrencySymbol} {Ui.Money(totals.GrandTotal)}";
    }

    private CartLine? SelectedLine =>
        _cart.CurrentRow is { Index: var i } && i >= 0 && i < _draft.Lines.Count ? _draft.Lines[i] : null;

    private void SetOrderType(OrderType type)
    {
        _draft.OrderType = type;
        if (type == OrderType.Delivery && _draft.DeliveryCharge == 0) _draft.DeliveryCharge = _settings.DefaultDeliveryCharge;
        if (type != OrderType.Delivery) _draft.DeliveryCharge = 0;
        if (type != OrderType.DineIn)
        {
            _draft.TableId = null;
            _draft.TableName = null;
        }
        RefreshOrder();

        if (type == OrderType.Delivery && string.IsNullOrWhiteSpace(_draft.CustomerPhone)) EditCustomer();
    }

    private void AddItem(MenuItem item)
    {
        var line = _draft.Lines.FirstOrDefault(l => l.MenuItemId == item.Id && string.IsNullOrEmpty(l.Notes));
        if (line is null)
        {
            line = new CartLine
            {
                MenuItemId = item.Id,
                Name = item.Name,
                KitchenSection = item.Category?.KitchenSection ?? "KITCHEN",
                UnitPrice = item.Price,
                Quantity = 1
            };
            _draft.Lines.Add(line);
        }
        else
        {
            line.Quantity++;
        }
        RenderCart(_draft.Lines.IndexOf(line));
    }

    private bool CanVoid(CartLine line)
    {
        if (!_session.RequireUser().CanManage)
        {
            Ui.Warn(this, "This item has already been sent to the kitchen.\nOnly a Manager or Admin can void it.");
            return false;
        }
        return Ui.Confirm(this, $"'{line.Name}' was already sent to the kitchen.\nA cancellation will be printed on the next KOT. Continue?");
    }

    private void ChangeQuantity(int delta)
    {
        var line = SelectedLine;
        if (line is null) return;

        var quantity = line.Quantity + delta;
        if (delta < 0 && quantity < line.PrintedQuantity && !CanVoid(line)) return;

        var index = _draft.Lines.IndexOf(line);
        if (quantity <= 0) _draft.Lines.Remove(line);
        else line.Quantity = quantity;
        RenderCart(index);
    }

    private void RemoveLine()
    {
        var line = SelectedLine;
        if (line is null) return;
        if (line.PrintedQuantity > 0 && !CanVoid(line)) return;

        var index = _draft.Lines.IndexOf(line);
        _draft.Lines.Remove(line);
        RenderCart(index);
    }

    private void EditNote()
    {
        var line = SelectedLine;
        if (line is null) return;
        var note = InputDialog.Prompt(this, "Kitchen Note", $"Note for {line.Name} (e.g. less spicy, no onion):", line.Notes ?? string.Empty);
        if (note is null) return;
        line.Notes = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        RenderCart();
    }

    private void EditDiscount()
    {
        if (_draft.Lines.Count == 0) return;
        if (!_session.RequireUser().CanManage)
        {
            Ui.Warn(this, "Only a Manager or Admin can apply discounts.");
            return;
        }

        var totals = OrderCalculator.Calculate(_draft, _settings.CashTaxPercent, _settings.ServiceChargePercent);
        using var dialog = new DiscountDialog(_draft.DiscountPercent, _draft.DiscountAmount, totals.SubTotal);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _draft.DiscountPercent = dialog.DiscountPercent;
        _draft.DiscountAmount = dialog.DiscountAmount;
        RenderTotals();
    }

    private void EditCustomer()
    {
        var isDelivery = _draft.OrderType == OrderType.Delivery;
        using var dialog = new CustomerDialog(_customers, isDelivery, _draft.CustomerPhone, _draft.CustomerName, _draft.DeliveryAddress, _draft.DeliveryCharge);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _draft.CustomerPhone = dialog.CustomerPhone;
        _draft.CustomerName = dialog.CustomerName;
        if (isDelivery)
        {
            _draft.DeliveryAddress = dialog.DeliveryAddress;
            _draft.DeliveryCharge = dialog.DeliveryCharge;
        }
        RefreshOrder();
    }

    private void ClearScreen()
    {
        if (_draft.Lines.Count > 0 && !Ui.Confirm(this, "Clear the screen? Any unsaved changes will be lost.")) return;
        NewOrder();
    }

    private void NewOrder()
    {
        var type = _draft.OrderType;
        _draft = new OrderDraft
        {
            OrderType = type,
            DeliveryCharge = type == OrderType.Delivery ? _settings.DefaultDeliveryCharge : 0
        };
        _search.Clear();
        RefreshOrder();
    }

    private async Task ReloadAsync(int orderId)
    {
        _draft = await _orders.GetDraftAsync(orderId);
        RefreshOrder();
    }

    private async Task SelectTableAsync()
    {
        if (_draft.OrderType != OrderType.DineIn) return;

        using var dialog = _forms.Create<TableSelectForm>();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedTable is not { } table) return;

        if (table.IsOccupied && table.OpenOrderId != _draft.OrderId)
        {
            if (_draft.Lines.Count > 0 && !Ui.Confirm(this, $"Table {table.Name} has a running order.\nDiscard the current screen and open it?")) return;
            await this.RunAsync(() => ReloadAsync(table.OpenOrderId!.Value));
            return;
        }

        _draft.TableId = table.TableId;
        _draft.TableName = table.Name;
        RefreshOrder();
    }

    private async Task OpenRunningOrdersAsync()
    {
        using var dialog = _forms.Create<RunningOrdersForm>();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedOrderId is not int id) return;
        if (_draft.Lines.Count > 0 && _draft.OrderId != id && !Ui.Confirm(this, "Discard the current screen and open the selected order?")) return;
        await this.RunAsync(() => ReloadAsync(id));
    }

    private bool EnsureItems()
    {
        if (_draft.Lines.Count > 0) return true;
        Ui.Warn(this, "Please add items to the order first.");
        return false;
    }

    private async Task PrintSafeAsync(Func<Task> print, string document)
    {
        try
        {
            await print();
        }
        catch (Exception ex)
        {
            Ui.Warn(this, $"The order was saved, but the {document} could not be printed.\n\n{ex.Message}");
        }
    }

    private void SetStatus(string message) => _status.Text = $"{DateTime.Now:hh:mm:ss tt}  -  {message}";

    private async Task HoldAsync()
    {
        if (!EnsureItems()) return;
        await this.RunAsync(async () =>
        {
            await _orders.SaveAsync(_draft);
            SetStatus("Order saved as running order.");
            NewOrder();
        });
    }

    private async Task SendKotAsync()
    {
        if (!EnsureItems()) return;
        await this.RunAsync(async () =>
        {
            var result = await _orders.SendToKitchenAsync(_draft);
            if (result.Ticket is null)
            {
                SetStatus("No new items to send to the kitchen. Order saved.");
            }
            else
            {
                await PrintSafeAsync(() => _printer.PrintKotAsync(result.Ticket), "KOT");
                SetStatus($"KOT #{result.Ticket.KotNumber} sent to kitchen for order #{result.Ticket.OrderNo}.");
            }

            if (_draft.OrderType == OrderType.DineIn) NewOrder();
            else await ReloadAsync(result.OrderId);
        });
    }

    private async Task PrintBillAsync()
    {
        if (!EnsureItems()) return;
        await this.RunAsync(async () =>
        {
            var orderId = await _orders.SaveAsync(_draft);
            var bill = await _orders.BuildBillAsync(orderId, PaymentMethod.Cash);
            await PrintSafeAsync(() => _printer.PrintInvoiceAsync(bill), "bill");
            await ReloadAsync(orderId);
            SetStatus($"Guest bill printed for order #{bill.OrderNo}.");
        });
    }

    private async Task PayAsync()
    {
        if (!EnsureItems()) return;
        await this.RunAsync(async () =>
        {
            var kitchen = await _orders.SendToKitchenAsync(_draft);
            if (kitchen.Ticket is not null)
                await PrintSafeAsync(() => _printer.PrintKotAsync(kitchen.Ticket), "KOT");

            var orderId = kitchen.OrderId;
            await ReloadAsync(orderId);

            var totals = new Dictionary<PaymentMethod, OrderTotals>();
            foreach (var method in Enum.GetValues<PaymentMethod>())
                totals[method] = await _orders.GetTotalsAsync(orderId, method);

            using var dialog = new PaymentForm(totals, _settings.CurrencySymbol, $"Order #{_draft.OrderNo}");
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                SetStatus("Payment cancelled. The order is still running.");
                return;
            }

            var invoice = await _orders.CompleteAsync(orderId, dialog.Method, dialog.Received);
            await PrintSafeAsync(() => _printer.PrintInvoiceAsync(invoice), "invoice");
            SetStatus($"Order #{invoice.OrderNo} settled by {invoice.PaymentMethod}. Total {Ui.Money(invoice.GrandTotal)}  |  Change {Ui.Money(invoice.Change)}");
            NewOrder();
        });
    }

    private async Task CancelOrderAsync()
    {
        if (_draft.OrderId is not int orderId)
        {
            if (_draft.Lines.Count == 0 || Ui.Confirm(this, "Discard this new order?")) NewOrder();
            return;
        }

        if (!_session.RequireUser().CanManage)
        {
            Ui.Warn(this, "Only a Manager or Admin can cancel a running order.");
            return;
        }

        var reason = InputDialog.Prompt(this, "Cancel Order", $"Reason for cancelling order #{_draft.OrderNo}:");
        if (string.IsNullOrWhiteSpace(reason)) return;

        await this.RunAsync(async () =>
        {
            var ticket = await _orders.CancelAsync(orderId, reason);
            if (ticket is not null)
                await PrintSafeAsync(() => _printer.PrintKotAsync(ticket), "cancellation KOT");
            SetStatus($"Order #{_draft.OrderNo} cancelled.");
            NewOrder();
        });
    }

    private async Task ChangePasswordAsync()
    {
        var current = InputDialog.Prompt(this, "Change Password", "Current password:", password: true);
        if (current is null) return;
        var next = InputDialog.Prompt(this, "Change Password", "New password:", password: true);
        if (next is null) return;
        var confirm = InputDialog.Prompt(this, "Change Password", "Confirm new password:", password: true);
        if (confirm is null) return;
        if (next != confirm)
        {
            Ui.Warn(this, "New passwords do not match.");
            return;
        }

        await this.RunAsync(async () =>
        {
            await _auth.ChangePasswordAsync(current, next);
            Ui.Info(this, "Password changed successfully.");
        });
    }

    private void Logout()
    {
        if (!Ui.Confirm(this, $"Log out of {Brand.Product}?")) return;
        LogoutRequested = true;
        _auth.Logout();
        Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (LogoutRequested) return;
        if (!Ui.Confirm(this, $"Exit {Brand.Product}?"))
        {
            e.Cancel = true;
            return;
        }
        _auth.Logout();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.F1: ClearScreen(); return true;
            case Keys.F2: _search.Focus(); _search.SelectAll(); return true;
            case Keys.F4: _ = HoldAsync(); return true;
            case Keys.F5: _ = SendKotAsync(); return true;
            case Keys.F6: _ = OpenRunningOrdersAsync(); return true;
            case Keys.F7: _ = SelectTableAsync(); return true;
            case Keys.F8: _ = PrintBillAsync(); return true;
            case Keys.F9: _ = PayAsync(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
