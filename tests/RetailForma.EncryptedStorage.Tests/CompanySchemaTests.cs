using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RetailForma.Core.Data;
using RetailForma.DataService;
using RetailForma.EncryptedStorage;

namespace RetailForma.EncryptedStorage.Tests;

public sealed class CompanySchemaTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "RetailForma-schema-qualification-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    private readonly RetailFormaDbContext db;
    public CompanySchemaTests()
    {
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "isolated.db");
        using (SqlCipherConnections.CreateNew(path, key)) { }
        db = EncryptedDbContexts.OpenExisting(path, key); DbInitializer.Initialize(db);
    }
    [Fact]
    public void EmptyAccountsCannotTriggerStarterAccountSubstitutionDuringRestore()
    {
        db.Users.RemoveRange(db.Users); db.SaveChanges();
        Assert.Throws<InvalidDataException>(() => CompanySchema.BeforeMigration(db)); Assert.Empty(db.Users);
    }
    [Fact]
    public void FutureMigrationAndExecutableSchemaAreRefusedBeforeAnyMigration()
    {
        db.Database.ExecuteSqlRaw("INSERT INTO __EFMigrationsHistory (MigrationId,ProductVersion) VALUES ('99999999999999_Future','99.0.0');");
        Assert.Throws<InvalidDataException>(() => CompanySchema.BeforeMigration(db));
        db.Database.ExecuteSqlRaw("DELETE FROM __EFMigrationsHistory WHERE MigrationId='99999999999999_Future'; CREATE TRIGGER Unexpected AFTER UPDATE ON Users BEGIN SELECT 1; END;");
        Assert.Throws<InvalidDataException>(() => CompanySchema.BeforeMigration(db));
    }
    [Fact]
    public void MissingModelColumnsAndBrokenForeignKeysFailQualification()
    {
        CompanySchema.BeforeMigration(db); CompanySchema.AfterMigration(db);
        db.Database.ExecuteSqlRaw("DROP TABLE Products;");
        Assert.Throws<InvalidDataException>(() => CompanySchema.AfterMigration(db));
        db.Database.ExecuteSqlRaw("PRAGMA foreign_keys=OFF; CREATE TABLE Parent(Id INTEGER PRIMARY KEY); CREATE TABLE Child(ParentId INTEGER REFERENCES Parent(Id)); INSERT INTO Child VALUES(999); PRAGMA foreign_keys=ON;");
        Assert.Throws<InvalidDataException>(() => SqlCipherConnections.VerifyIntegrity((Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection()));
    }
    public void Dispose() { db.Dispose(); CryptographicOperations.ZeroMemory(key); Directory.Delete(directory, true); }
}
