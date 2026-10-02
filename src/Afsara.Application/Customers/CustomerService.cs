using Afsara.Application.Common.Interfaces;
using Afsara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Afsara.Application.Customers;

public interface ICustomerService
{
    Task<Customer?> FindByPhoneAsync(string phone);
}

public class CustomerService : ICustomerService
{
    private readonly IApplicationDbContextFactory _factory;

    public CustomerService(IApplicationDbContextFactory factory) => _factory = factory;

    public async Task<Customer?> FindByPhoneAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        await using var db = _factory.Create();
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Phone == phone.Trim());
    }
}
