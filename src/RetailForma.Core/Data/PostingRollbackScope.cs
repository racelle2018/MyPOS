using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace RetailForma.Core.Data;

// A database rollback alone does not undo EF's in-memory mutations. Preserve pending
// edits that existed before the operation, and discard only the failed posting.
internal sealed class PostingRollbackScope : IDisposable
{
    private readonly RetailFormaDbContext db;
    private readonly Dictionary<object, (EntityState State, PropertyValues Current, PropertyValues Original)> before;
    private readonly HashSet<object> created = new(ReferenceEqualityComparer.Instance);
    private bool completed;
    public PostingRollbackScope(RetailFormaDbContext db)
    {
        this.db = db;
        db.ChangeTracker.DetectChanges();
        before = db.ChangeTracker.Entries().ToDictionary(e => e.Entity,
            e => (e.State, e.CurrentValues.Clone(), e.OriginalValues.Clone()), ReferenceEqualityComparer.Instance);
        db.ChangeTracker.Tracked += OnTracked;
    }
    private void OnTracked(object? sender, EntityTrackedEventArgs args)
    { if (args.Entry.State == EntityState.Added) created.Add(args.Entry.Entity); }
    public void Complete() => completed = true;
    public void Refresh(object entity)
    {
        var entry = db.Entry(entity);
        before[entity] = (entry.State, entry.CurrentValues.Clone(), entry.OriginalValues.Clone());
    }
    public void Dispose()
    {
        db.ChangeTracker.Tracked -= OnTracked;
        if (completed) return;
        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            if (before.TryGetValue(entry.Entity, out var saved))
            {
                entry.State = EntityState.Unchanged;
                entry.CurrentValues.SetValues(saved.Current);
                entry.OriginalValues.SetValues(saved.Original);
                entry.State = saved.State;
            }
            else if (created.Contains(entry.Entity) || entry.State == EntityState.Added) entry.State = EntityState.Detached;
            else if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                entry.CurrentValues.SetValues(entry.OriginalValues);
                entry.State = EntityState.Unchanged;
            }
        }
        foreach (var entry in db.ChangeTracker.Entries().ToList())
            foreach (var collection in entry.Collections)
                if (collection.CurrentValue is System.Collections.IList list)
                    for (var i = list.Count - 1; i >= 0; i--)
                        if (list[i] is { } item && created.Contains(item)) list.RemoveAt(i);
    }
}
