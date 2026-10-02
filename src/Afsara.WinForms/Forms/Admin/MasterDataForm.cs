using Afsara.Application.Catalog;
using Afsara.Application.Common;
using Afsara.Application.Users;
using Afsara.Domain.Enums;
using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Admin;

public class MasterDataForm : Form
{
    public MasterDataForm(ICatalogService catalog, IUserService users, IUserSession session)
    {
        Theme.ApplyDialog(this, "Setup - Menu, Tables, Waiters & Users", new Size(1180, 700));
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimumSize = new Size(960, 560);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.Font(11f, FontStyle.Bold), Padding = new Point(18, 8) };
        Controls.Add(tabs);

        void AddTab(string title, Control content)
        {
            var page = new TabPage(title) { BackColor = Theme.Background, Font = Theme.Font(10f) };
            page.Controls.Add(content);
            tabs.TabPages.Add(page);
        }

        AddTab("Menu Items", new MenuItemsPanel(catalog));
        AddTab("Categories", new CategoriesPanel(catalog));
        AddTab("Tables", new TablesPanel(catalog));
        AddTab("Waiters", new WaitersPanel(catalog));
        if (session.CurrentUser?.Role == UserRole.Admin)
            AddTab("Users", new UsersPanel(users));
    }
}
