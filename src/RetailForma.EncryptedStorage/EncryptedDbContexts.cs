using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;

namespace RetailForma.EncryptedStorage;

public static class EncryptedDbContexts
{
    public static RetailFormaDbContext OpenExisting(string path, ReadOnlySpan<byte> key)
    {
        var connection = SqlCipherConnections.OpenExisting(path, key);
        try
        {
            var context = new RetailFormaDbContext(new DbContextOptionsBuilder<RetailFormaDbContext>()
                .UseSqlite(connection, contextOwnsConnection: true).Options);
            // Initialize ownership even if the operation is rejected before executing any EF query.
            _ = context.Database.GetDbConnection();
            return context;
        }
        catch { connection.Dispose(); throw; }
    }
}
