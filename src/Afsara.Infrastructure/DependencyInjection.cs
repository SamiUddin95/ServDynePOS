using Afsara.Application.Common.Interfaces;
using Afsara.Application.Licensing;
using Afsara.Application.Reports;
using Afsara.Infrastructure.Licensing;
using Afsara.Infrastructure.Reports;
using Afsara.Infrastructure.Persistence;
using Afsara.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Afsara.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing in appsettings.json.");

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3)));

        services.AddSingleton<IApplicationDbContextFactory, AppDbContextFactory>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<IReportExporter, ReportExporter>();
        services.AddSingleton<ILicenseService, LicenseService>();
        return services;
    }
}
