using Afsara.Application;
using Afsara.Application.Common.Interfaces;
using Afsara.Application.Licensing;
using Afsara.Infrastructure;
using Afsara.Printing;
using Afsara.WinForms.Common;
using Afsara.WinForms.Forms;
using Afsara.WinForms.Forms.Dialogs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Afsara.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddPrinting();
        services.AddSingleton<FormFactory>();

        using var provider = services.BuildServiceProvider();

        try
        {
            Task.Run(() => provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync())
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            var inner = ex;
            while (inner.InnerException is not null) inner = inner.InnerException;
            MessageBox.Show(
                $"Unable to connect to or initialize the database.\n\n{inner.Message}\n\nCheck the connection string in appsettings.json.",
                $"{Brand.Product} - Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var forms = provider.GetRequiredService<FormFactory>();
        var license = provider.GetRequiredService<ILicenseService>();
        while (true)
        {
            if (!Task.Run(license.GetStatusAsync).GetAwaiter().GetResult().IsValid)
            {
                using var activation = forms.Create<LicenseForm>();
                if (activation.ShowDialog() != DialogResult.OK) break;
            }

            using var login = forms.Create<LoginForm>();
            if (login.ShowDialog() != DialogResult.OK) break;

            using var main = forms.Create<MainForm>();
            System.Windows.Forms.Application.Run(main);
            if (!main.LogoutRequested) break;
        }
    }
}
