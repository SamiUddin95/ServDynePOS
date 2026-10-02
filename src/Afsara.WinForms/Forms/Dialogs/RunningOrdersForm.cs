using Afsara.Application.Common;
using Afsara.Application.Orders;
using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Dialogs;

public class RunningOrdersForm : Form
{
    private readonly IOrderService _orders;
    private readonly DataGridView _grid = new();
    private readonly Label _summary;
    private List<OpenOrderSummary> _data = new();

    public int? SelectedOrderId { get; private set; }

    public RunningOrdersForm(IOrderService orders)
    {
        _orders = orders;
        Theme.ApplyDialog(this, "Running Orders", new Size(980, 580));
        FormBorderStyle = FormBorderStyle.Sizable;

        Theme.StyleGrid(_grid);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OpenSelected(); };
        Controls.Add(_grid);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 60, ColumnCount = 4, Padding = new Padding(8) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        _summary = Theme.Label(string.Empty, 11f, FontStyle.Bold);
        bottom.Controls.Add(_summary, 0, 0);
        var refresh = Theme.OutlineButton("Refresh");
        refresh.Click += async (_, _) => await LoadOrdersAsync();
        bottom.Controls.Add(refresh, 1, 0);
        var close = Theme.OutlineButton("Close");
        close.DialogResult = DialogResult.Cancel;
        bottom.Controls.Add(close, 2, 0);
        var open = Theme.Button("OPEN ORDER", Theme.Primary);
        open.Click += (_, _) => OpenSelected();
        bottom.Controls.Add(open, 3, 0);
        Controls.Add(bottom);

        CancelButton = close;
        Load += async (_, _) => await LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        await this.RunAsync(async () =>
        {
            _data = await _orders.GetOpenOrdersAsync();
            _grid.DataSource = _data.Select(o => new
            {
                o.Id,
                Order = $"#{o.OrderNo}",
                Type = o.OrderType.ToDisplay(),
                Table = o.TableName ?? "-",
                Waiter = o.WaiterName ?? "-",
                Customer = o.CustomerName ?? "-",
                Qty = o.Items,
                Amount = Ui.Money(o.GrandTotal),
                Time = o.CreatedAt.ToString("hh:mm tt"),
                Minutes = (int)(DateTime.Now - o.CreatedAt).TotalMinutes
            }).ToList();
            if (_grid.Columns["Id"] is { } id) id.Visible = false;
            _summary.Text = $"{_data.Count} running order(s)  |  Total: {Ui.Money(_data.Sum(o => o.GrandTotal))}";
        });
    }

    private void OpenSelected()
    {
        if (_grid.CurrentRow is null) return;
        SelectedOrderId = (int)_grid.CurrentRow.Cells["Id"].Value;
        DialogResult = DialogResult.OK;
    }
}
