using Afsara.Application.Catalog;
using Afsara.Domain.Entities;
using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Admin;

public class CategoriesPanel : CrudPanel
{
    private readonly ICatalogService _catalog;
    private readonly TextBox _name = Theme.TextBox();
    private readonly ComboBox _section = Theme.Combo();
    private readonly NumericUpDown _order = Theme.Numeric(999);
    private readonly CheckBox _active = CheckBox("Active");
    private List<Category> _data = new();

    public CategoriesPanel(ICatalogService catalog) : base("Category")
    {
        _catalog = catalog;
        _section.DropDownStyle = ComboBoxStyle.DropDown;
        _section.Items.AddRange(new object[] { "KITCHEN", "BBQ", "TANDOOR", "FAST FOOD", "BAR" });
        AddField("Category Name *", _name);
        AddField("Kitchen Section (printed on KOT)", _section);
        AddField("Display Order", _order);
        AddField(string.Empty, _active, 30);
    }

    protected override async Task<object> LoadGridAsync()
    {
        _data = await _catalog.GetCategoriesAsync();
        return _data.Select(c => new { c.Id, c.Name, Section = c.KitchenSection, Order = c.DisplayOrder, Active = c.IsActive ? "Yes" : "No" }).ToList();
    }

    protected override void Bind(int id)
    {
        var c = _data.FirstOrDefault(x => x.Id == id) ?? new Category { DisplayOrder = _data.Count + 1 };
        _name.Text = c.Name;
        _section.Text = c.KitchenSection;
        _order.Value = Math.Clamp(c.DisplayOrder, 0, 999);
        _active.Checked = c.IsActive;
        _name.Focus();
    }

    protected override Task SaveAsync(int id) => _catalog.SaveCategoryAsync(new Category
    {
        Id = id,
        Name = _name.Text,
        KitchenSection = _section.Text,
        DisplayOrder = (int)_order.Value,
        IsActive = _active.Checked
    });

    protected override Task DeleteAsync(int id) => _catalog.DeleteCategoryAsync(id);
}

public class MenuItemsPanel : CrudPanel
{
    private readonly ICatalogService _catalog;
    private readonly TextBox _code = Theme.TextBox();
    private readonly TextBox _name = Theme.TextBox();
    private readonly ComboBox _category = Theme.Combo();
    private readonly NumericUpDown _price = Theme.Numeric(1_000_000);
    private readonly CheckBox _active = CheckBox("Active (visible on POS)");
    private List<MenuItem> _data = new();

    public MenuItemsPanel(ICatalogService catalog) : base("Menu Item")
    {
        _catalog = catalog;
        AddField("Item Code", _code);
        AddField("Item Name *", _name);
        AddField("Category *", _category);
        AddField("Sale Price *", _price);
        AddField(string.Empty, _active, 30);
    }

    protected override async Task<object> LoadGridAsync()
    {
        var categories = await _catalog.GetCategoriesAsync();
        _category.Items.Clear();
        foreach (var c in categories) _category.Items.Add(new ComboItem(c.Id, c.Name));

        _data = await _catalog.GetMenuItemsAsync();
        return _data.Select(m => new
        {
            m.Id,
            m.Code,
            m.Name,
            Category = m.Category?.Name,
            Price = Ui.Money(m.Price),
            Active = m.IsActive ? "Yes" : "No"
        }).ToList();
    }

    protected override void Bind(int id)
    {
        var item = _data.FirstOrDefault(x => x.Id == id);
        _code.Text = item?.Code ?? string.Empty;
        _name.Text = item?.Name ?? string.Empty;
        _price.Value = item?.Price ?? 0;
        _active.Checked = item?.IsActive ?? true;

        var categoryId = item?.CategoryId ?? (_category.SelectedItem as ComboItem)?.Id;
        _category.SelectedItem = _category.Items.Cast<ComboItem>().FirstOrDefault(c => c.Id == categoryId)
            ?? _category.Items.Cast<ComboItem>().FirstOrDefault();
        _name.Focus();
    }

    protected override Task SaveAsync(int id) => _catalog.SaveMenuItemAsync(new MenuItem
    {
        Id = id,
        Code = _code.Text,
        Name = _name.Text,
        CategoryId = (_category.SelectedItem as ComboItem)?.Id ?? 0,
        Price = _price.Value,
        IsActive = _active.Checked
    });

    protected override Task DeleteAsync(int id) => _catalog.DeleteMenuItemAsync(id);
}

public class TablesPanel : CrudPanel
{
    private readonly ICatalogService _catalog;
    private readonly TextBox _name = Theme.TextBox();
    private readonly ComboBox _area = Theme.Combo();
    private readonly NumericUpDown _capacity = Theme.Numeric(50);
    private readonly CheckBox _active = CheckBox("Active");
    private List<DiningTable> _data = new();

    public TablesPanel(ICatalogService catalog) : base("Table")
    {
        _catalog = catalog;
        _area.DropDownStyle = ComboBoxStyle.DropDown;
        _capacity.Minimum = 1;
        AddField("Table Name *", _name);
        AddField("Area / Hall", _area);
        AddField("Seating Capacity", _capacity);
        AddField(string.Empty, _active, 30);
    }

    protected override async Task<object> LoadGridAsync()
    {
        _data = await _catalog.GetTablesAsync();
        _area.Items.Clear();
        _area.Items.AddRange(_data.Select(t => t.Area).Distinct().Cast<object>().ToArray());
        return _data.Select(t => new { t.Id, Table = t.Name, t.Area, Seats = t.Capacity, Active = t.IsActive ? "Yes" : "No" }).ToList();
    }

    protected override void Bind(int id)
    {
        var t = _data.FirstOrDefault(x => x.Id == id) ?? new DiningTable();
        _name.Text = t.Name;
        _area.Text = t.Area;
        _capacity.Value = Math.Clamp(t.Capacity, 1, 50);
        _active.Checked = t.IsActive;
        _name.Focus();
    }

    protected override Task SaveAsync(int id) => _catalog.SaveTableAsync(new DiningTable
    {
        Id = id,
        Name = _name.Text,
        Area = _area.Text,
        Capacity = (int)_capacity.Value,
        IsActive = _active.Checked
    });

    protected override Task DeleteAsync(int id) => _catalog.DeleteTableAsync(id);
}

public class WaitersPanel : CrudPanel
{
    private readonly ICatalogService _catalog;
    private readonly TextBox _name = Theme.TextBox();
    private readonly TextBox _phone = Theme.TextBox();
    private readonly CheckBox _active = CheckBox("Active");
    private List<Waiter> _data = new();

    public WaitersPanel(ICatalogService catalog) : base("Waiter")
    {
        _catalog = catalog;
        AddField("Waiter Name *", _name);
        AddField("Phone", _phone);
        AddField(string.Empty, _active, 30);
    }

    protected override async Task<object> LoadGridAsync()
    {
        _data = await _catalog.GetWaitersAsync();
        return _data.Select(w => new { w.Id, w.Name, w.Phone, Active = w.IsActive ? "Yes" : "No" }).ToList();
    }

    protected override void Bind(int id)
    {
        var w = _data.FirstOrDefault(x => x.Id == id) ?? new Waiter();
        _name.Text = w.Name;
        _phone.Text = w.Phone ?? string.Empty;
        _active.Checked = w.IsActive;
        _name.Focus();
    }

    protected override Task SaveAsync(int id) => _catalog.SaveWaiterAsync(new Waiter
    {
        Id = id,
        Name = _name.Text,
        Phone = _phone.Text,
        IsActive = _active.Checked
    });

    protected override Task DeleteAsync(int id) => _catalog.DeleteWaiterAsync(id);
}
