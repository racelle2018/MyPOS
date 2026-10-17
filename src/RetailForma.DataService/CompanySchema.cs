using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RetailForma.Core.Data;

namespace RetailForma.DataService;

internal static class CompanySchema
{
    public static void BeforeMigration(RetailFormaDbContext db)
    {
        var known = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        if (db.Database.GetAppliedMigrations().Any(id => !known.Contains(id)))
            throw new InvalidDataException("The backup contains a newer or unrecognized database migration. Update RetailForma or review the archive before importing it.");
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type IN ('trigger','view') OR sql LIKE '%CREATE VIRTUAL TABLE%';";
        if (Convert.ToInt64(command.ExecuteScalar()) != 0)
            throw new InvalidDataException("The backup contains unsupported executable schema objects. Review it before restoring.");
        if (!db.Users.Any()) throw new InvalidDataException("The backup has no company accounts. Recovery cannot substitute known starter accounts.");
    }

    public static void AfterMigration(RetailFormaDbContext db)
    {
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName(); if (table == null) continue;
            using var command = db.Database.GetDbConnection().CreateCommand();
            // Names come only from the compiled model, never from the uploaded file or IPC request.
            command.CommandText = "SELECT name FROM pragma_table_info($table);"; var parameter = command.CreateParameter();
            parameter.ParameterName = "$table"; parameter.Value = table; command.Parameters.Add(parameter);
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = command.ExecuteReader()) while (reader.Read()) columns.Add(reader.GetString(0));
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            if (entity.GetProperties().Select(p => p.GetColumnName(store)).Any(column => column != null && !columns.Contains(column)))
                throw new InvalidDataException("The backup is missing required company schema. No live database was replaced.");
        }
    }
}
