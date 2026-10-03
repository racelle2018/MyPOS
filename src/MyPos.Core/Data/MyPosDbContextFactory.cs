using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyPos.Core.Data;

public sealed class MyPosDbContextFactory : IDesignTimeDbContextFactory<MyPosDbContext>
{
    public MyPosDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MyPosDbContext>()
            .UseSqlite("Data Source=mypos.design.db")
            .Options;
        return new MyPosDbContext(options);
    }
}
