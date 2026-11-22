using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CivicConnect.Api.Data;

public sealed class CivicConnectDbContextFactory : IDesignTimeDbContextFactory<CivicConnectDbContext>
{
    public CivicConnectDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CivicConnectDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=civicconnect;Username=civicconnect;Password=civicconnect_dev")
            .Options;
        return new CivicConnectDbContext(options);
    }
}
