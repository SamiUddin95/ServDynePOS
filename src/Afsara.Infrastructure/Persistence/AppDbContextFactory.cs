using Afsara.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Afsara.Infrastructure.Persistence;

public class AppDbContextFactory : IApplicationDbContextFactory
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AppDbContextFactory(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public IApplicationDbContext Create() => _factory.CreateDbContext();
}

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AFSARA_POS_CONNECTION")
            ?? "Server=.;Database=AfsaraPOS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
