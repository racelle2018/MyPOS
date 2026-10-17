using RetailForma.Desktop;
using Xunit;

namespace RetailForma.Desktop.Tests;

public sealed class BackupRetentionTests
{
    [Fact]
    public void Retains_thirty_daily_eight_weekly_and_pending_cloud_snapshots()
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        var snapshots = Enumerable.Range(0, 120).Select(i => new BackupRetention.Snapshot(
            $"backup-{i}", now.AddHours(-12 * i), i == 119)).ToArray();
        var deleted = BackupRetention.Eligible(snapshots);
        Assert.DoesNotContain(deleted, s => s.Path == "backup-0");
        Assert.DoesNotContain(deleted, s => s.PendingCloud);
        var retained = snapshots.Except(deleted).ToArray();
        Assert.Equal(30, retained.Where(s => s.CreatedUtc >= now.AddDays(-29)).Select(s => s.CreatedUtc.Date).Distinct().Count());
        Assert.Contains(deleted, s => s.Path == "backup-118");
        Assert.True(retained.Length >= 30);
    }

    [Fact]
    public void Single_verified_copy_and_sparse_history_are_preserved()
    {
        var one = new BackupRetention.Snapshot("only", DateTimeOffset.UtcNow, false);
        Assert.Empty(BackupRetention.Eligible([one]));
        Assert.Empty(BackupRetention.Eligible([]));
        var sparse = Enumerable.Range(0, 30).Select(i => new BackupRetention.Snapshot(
            i.ToString(), DateTimeOffset.UtcNow.AddDays(-30 * i), false)).ToArray();
        Assert.Empty(BackupRetention.Eligible(sparse));
    }
}
