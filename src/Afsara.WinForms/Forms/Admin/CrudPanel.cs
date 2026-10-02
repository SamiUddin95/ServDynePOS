using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Admin;

public abstract class CrudPanel : UserControl
{
    protected readonly DataGridView Grid = new();
    private readonly TableLayoutPanel _editor;
    private readonly Label _editorTitle;
    private readonly string _entityName;
    private int _row;
    private bool _loading;
    private bool _initialized;

    protected int SelectedId { get; private set; }

    protected CrudPanel(string entityName)
    {
        _entityName = entityName;
        Dock = DockStyle.Fill;
        BackColor = Theme.Background;
        Padding = new Padding(8);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        Controls.Add(layout);

        Theme.StyleGrid(Grid);
        Grid.Margin = new Padding(0, 0, 8, 0);
        Grid.SelectionChanged += (_, _) =>
        {
            if (!_loading && Grid.CurrentRow is not null && Grid.CurrentRow.Selected) SelectRow();
        };
        layout.Controls.Add(Grid, 0, 0);

        var side = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(14), RowCount = 3, Margin = Padding.Empty };
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.Controls.Add(side, 1, 0);

        _editorTitle = Theme.Label($"New {entityName}", 13f, FontStyle.Bold);
        side.Controls.Add(_editorTitle, 0, 0);

        _editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true };
        side.Controls.Add(_editor, 0, 1);

        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var add = Theme.Button("+ New", Theme.Primary);
        add.Click += (_, _) => BeginNew();
        var save = Theme.Button("Save", Theme.Success);
        save.Click += async (_, _) => await SaveClickedAsync();
        var delete = Theme.Button("Delete", Theme.Danger);
        delete.Click += async (_, _) => await DeleteClickedAsync();
        buttons.Controls.Add(add, 0, 0);
        buttons.Controls.Add(save, 1, 0);
        buttons.Controls.Add(delete, 0, 1);
        buttons.SetColumnSpan(delete, 2);
        side.Controls.Add(buttons, 0, 2);
    }

    protected void AddField(string label, Control control, float height = 38)
    {
        _editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var caption = Theme.Label(label, 9.5f, FontStyle.Bold, Theme.Muted);
        caption.Margin = new Padding(3, 8, 3, 2);
        _editor.Controls.Add(caption, 0, _row++);
        _editor.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        control.Dock = DockStyle.Fill;
        _editor.Controls.Add(control, 0, _row++);
    }

    protected static CheckBox CheckBox(string text) => new() { Text = text, Font = Theme.Font(10.5f), Checked = true };

    protected abstract Task<object> LoadGridAsync();
    protected abstract void Bind(int id);
    protected abstract Task SaveAsync(int id);
    protected abstract Task DeleteAsync(int id);

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_initialized) return;
        _initialized = true;
        _editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _editor.Controls.Add(new Label(), 0, _row++);
        await this.RunAsync(() => ReloadAsync(0));
    }

    protected async Task ReloadAsync(int keepId)
    {
        _loading = true;
        Grid.DataSource = await LoadGridAsync();
        if (Grid.Columns["Id"] is { } idColumn) idColumn.Visible = false;
        _loading = false;

        var row = Grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => keepId > 0 && (int)r.Cells["Id"].Value == keepId);
        if (row is null)
        {
            BeginNew();
            return;
        }

        Grid.CurrentCell = row.Cells.Cast<DataGridViewCell>().First(c => c.Visible);
        row.Selected = true;
        SelectRow();
    }

    private void SelectRow()
    {
        if (Grid.CurrentRow?.Cells["Id"].Value is not int id) return;
        SelectedId = id;
        _editorTitle.Text = $"Edit {_entityName}";
        Bind(id);
    }

    private void BeginNew()
    {
        Grid.ClearSelection();
        SelectedId = 0;
        _editorTitle.Text = $"New {_entityName}";
        Bind(0);
    }

    private async Task SaveClickedAsync()
    {
        await this.RunAsync(async () =>
        {
            var id = SelectedId;
            await SaveAsync(id);
            await ReloadAsync(id);
            _editorTitle.Text = id == 0 ? $"{_entityName} added. Add another" : $"{_entityName} saved";
        });
    }

    private async Task DeleteClickedAsync()
    {
        if (SelectedId == 0) return;
        if (!Ui.Confirm(this, $"Delete the selected {_entityName.ToLowerInvariant()}?\nIf it is used in past orders it will be deactivated instead.")) return;
        await this.RunAsync(async () =>
        {
            await DeleteAsync(SelectedId);
            await ReloadAsync(0);
        });
    }
}
