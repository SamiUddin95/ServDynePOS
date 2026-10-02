using Afsara.Application.Users;
using Afsara.Domain.Entities;
using Afsara.Domain.Enums;
using Afsara.WinForms.Common;

namespace Afsara.WinForms.Forms.Admin;

public class UsersPanel : CrudPanel
{
    private readonly IUserService _users;
    private readonly TextBox _username = Theme.TextBox();
    private readonly TextBox _fullName = Theme.TextBox();
    private readonly ComboBox _role = Theme.Combo();
    private readonly TextBox _password = Theme.TextBox("Leave blank to keep current");
    private readonly CheckBox _active = CheckBox("Active (can sign in)");
    private List<User> _data = new();

    public UsersPanel(IUserService users) : base("User")
    {
        _users = users;
        _role.Items.AddRange(Enum.GetValues<UserRole>().Cast<object>().ToArray());
        _password.UseSystemPasswordChar = true;
        AddField("Username *", _username);
        AddField("Full Name *", _fullName);
        AddField("Role", _role);
        AddField("Password", _password);
        AddField(string.Empty, _active, 30);
    }

    protected override async Task<object> LoadGridAsync()
    {
        _data = await _users.GetUsersAsync();
        return _data.Select(u => new { u.Id, u.Username, Name = u.FullName, Role = u.Role.ToString(), Active = u.IsActive ? "Yes" : "No" }).ToList();
    }

    protected override void Bind(int id)
    {
        var u = _data.FirstOrDefault(x => x.Id == id) ?? new User();
        _username.Text = u.Username;
        _fullName.Text = u.FullName;
        _role.SelectedItem = u.Role;
        _password.Clear();
        _password.PlaceholderText = id == 0 ? "Required for new user" : "Leave blank to keep current";
        _active.Checked = u.IsActive;
        _username.Focus();
    }

    protected override Task SaveAsync(int id) => _users.SaveUserAsync(new User
    {
        Id = id,
        Username = _username.Text,
        FullName = _fullName.Text,
        Role = _role.SelectedItem is UserRole role ? role : UserRole.Cashier,
        IsActive = _active.Checked
    }, string.IsNullOrEmpty(_password.Text) ? null : _password.Text);

    protected override Task DeleteAsync(int id) => _users.DeleteUserAsync(id);
}
