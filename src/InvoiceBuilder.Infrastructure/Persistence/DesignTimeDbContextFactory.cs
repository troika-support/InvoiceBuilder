using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvoiceBuilder.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c> only. Connection string from IB_DESIGN_CONNECTION or LocalDB.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("IB_DESIGN_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=InvoiceBuilder;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        return new AppDbContext(options, new DevCurrentTenant(), new DevCurrentBusiness());
    }
}
