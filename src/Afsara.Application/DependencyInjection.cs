using Afsara.Application.Auth;
using Afsara.Application.Catalog;
using Afsara.Application.Common;
using Afsara.Application.Customers;
using Afsara.Application.Orders;
using Afsara.Application.Reports;
using Afsara.Application.Settings;
using Afsara.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Afsara.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IUserSession, UserSession>();
        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<ISettingsService, SettingsService>();
        services.AddTransient<ICatalogService, CatalogService>();
        services.AddTransient<IUserService, UserService>();
        services.AddTransient<ICustomerService, CustomerService>();
        services.AddTransient<IOrderService, OrderService>();
        services.AddTransient<IReportService, ReportService>();
        return services;
    }
}
