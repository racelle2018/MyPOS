using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RetailForma.Core.Data;

public sealed class RetailFormaDbContextFactory : IDesignTimeDbContextFactory<RetailFormaDbContext>
{
    public RetailFormaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RetailFormaDbContext>()
            .UseSqlite("Data Source=retailforma.design.db")
            .Options;
        return new RetailFormaDbContext(options);
    }
}
