using Afsara.Application.Common;

namespace Afsara.WinForms.Common;

public static class Ui
{
    private const string Caption = Brand.Product;

    public static async Task RunAsync(this Control owner, Func<Task> action)
    {
        var form = owner.FindForm() ?? owner as Form;
        if (form is not null) form.Cursor = Cursors.WaitCursor;
        try
        {
            await action();
        }
        catch (AppException ex)
        {
            Warn(owner, ex.Message);
        }
        catch (Exception ex)
        {
            Error(owner, ex);
        }
        finally
        {
            if (form is not null && !form.IsDisposed) form.Cursor = Cursors.Default;
        }
    }

    public static void Info(IWin32Window? owner, string message) =>
        MessageBox.Show(owner, message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Warn(IWin32Window? owner, string message) =>
        MessageBox.Show(owner, message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Error(IWin32Window? owner, Exception ex)
    {
        var inner = ex;
        while (inner.InnerException is not null) inner = inner.InnerException;
        MessageBox.Show(owner, inner.Message, $"{Caption} - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public static bool Confirm(IWin32Window? owner, string message) =>
        MessageBox.Show(owner, message, Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public static string Money(decimal value) => value.ToString("N0");
}

public sealed record ComboItem(int? Id, string Text)
{
    public override string ToString() => Text;
}
