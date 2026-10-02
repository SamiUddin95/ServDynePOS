using Afsara.Application.Orders;
using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Dialogs;

public class TableSelectForm : Form
{
    private readonly IOrderService _orders;
    private readonly FlowLayoutPanel _tables;

    public TableStatus? SelectedTable { get; private set; }

    public TableSelectForm(IOrderService orders)
    {
        _orders = orders;
        Theme.ApplyDialog(this, "Select Table", new Size(980, 640));
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(700, 480);

        _tables = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12), BackColor = Theme.Background };
        Controls.Add(_tables);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(12, 10, 12, 0), BackColor = Theme.Surface };
        top.Controls.Add(Legend("Available", Theme.Success));
        top.Controls.Add(Legend("Occupied (running order)", Theme.Danger));
        var refresh = Theme.OutlineButton("Refresh", 9.5f);
        refresh.Dock = DockStyle.None;
        refresh.Size = new Size(100, 32);
        refresh.Click += async (_, _) => await LoadTablesAsync();
        top.Controls.Add(refresh);
        Controls.Add(top);

        Load += async (_, _) => await LoadTablesAsync();
    }

    private async Task LoadTablesAsync()
    {
        await this.RunAsync(async () =>
        {
            var tables = await _orders.GetTableStatusesAsync();
            _tables.SuspendLayout();
            var old = _tables.Controls.Cast<Control>().ToList();
            _tables.Controls.Clear();
            old.ForEach(c => c.Dispose());

            foreach (var area in tables.GroupBy(t => t.Area))
            {
                var header = Theme.Label($"{area.Key}  ({area.Count(t => !t.IsOccupied)} free / {area.Count()})", 12f, FontStyle.Bold);
                header.Margin = new Padding(4, 12, 4, 6);
                _tables.Controls.Add(header);
                _tables.SetFlowBreak(header, true);

                Control? last = null;
                foreach (var table in area)
                {
                    last = CreateTile(table);
                    _tables.Controls.Add(last);
                }
                if (last is not null) _tables.SetFlowBreak(last, true);
            }
            _tables.ResumeLayout();
        });
    }

    private Button CreateTile(TableStatus table)
    {
        var color = table.IsOccupied ? Theme.Danger : Theme.Success;
        var detail = table.IsOccupied
            ? $"{Ui.Money(table.OpenAmount)}\n{(int)(DateTime.Now - table.OccupiedSince!.Value).TotalMinutes} min"
            : $"{table.Capacity} seats";

        var tile = Theme.Button($"{table.Name}\n{detail}", color, 12f);
        tile.Dock = DockStyle.None;
        tile.Size = new Size(130, 100);
        tile.Margin = new Padding(6);
        tile.Click += (_, _) =>
        {
            SelectedTable = table;
            DialogResult = DialogResult.OK;
        };
        return tile;
    }

    private static Control Legend(string text, Color color)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 4, 24, 0) };
        panel.Controls.Add(new Panel { BackColor = color, Size = new Size(18, 18), Margin = new Padding(0, 4, 6, 0) });
        panel.Controls.Add(Theme.Label(text, 10f));
        return panel;
    }
}
