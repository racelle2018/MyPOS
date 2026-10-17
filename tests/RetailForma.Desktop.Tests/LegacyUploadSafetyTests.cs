using System.IO;
using System.Reflection;
using RetailForma.Desktop;
using Xunit;

namespace RetailForma.Desktop.Tests;

public sealed class LegacyUploadSafetyTests
{
    [Theory]
    [InlineData("-wal")]
    [InlineData("-journal")]
    public void PendingLegacySidecarRefusesUploadBeforeAnyServiceRequest(string suffix)
    {
        var folder = Path.Combine(Path.GetTempPath(), "RetailForma-upload-qualification-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "legacy.db"); File.WriteAllBytes(path, new byte[512]); File.WriteAllBytes(path + suffix, new byte[32]);
        try
        {
            var adapter = typeof(App).Assembly.GetType("RetailForma.Desktop.ServiceBackupFiles", throwOnError: true)!;
            var upload = adapter.GetMethod("Upload", BindingFlags.Static | BindingFlags.Public)!;
            var rejected = Assert.Throws<TargetInvocationException>(() => upload.Invoke(null, [path]));
            Assert.IsType<InvalidOperationException>(rejected.InnerException); Assert.Contains("WAL/journal", rejected.InnerException!.Message);
        }
        finally { Directory.Delete(folder, true); }
    }
}
