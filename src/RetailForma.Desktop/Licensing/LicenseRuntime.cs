using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
namespace RetailForma.Desktop.Licensing;
public static class LicenseRuntime
{
    private static LicenseClient? client;
    private static bool configured;
    public static LicenseClient? Client
    {
        get
        {
            if (configured) return client;
            string? ReadKey(string name)
            {
                try
                {
                    var resource = Application.GetResourceStream(new Uri($"/RetailForma.Desktop;component/Licensing/{name}", UriKind.Relative));
                    if (resource == null) return null;
                    using var reader = new StreamReader(resource.Stream); return reader.ReadToEnd();
                }
                catch (IOException) { return null; }
            }
            var paid = ReadKey("paid-public.pem");
            if (paid != null)
            {
                var endpoint = typeof(App).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "RetailFormaTrialEndpoint")?.Value;
                var uri = string.IsNullOrWhiteSpace(endpoint) ? null : new Uri(endpoint, UriKind.Absolute);
                var cache = new LicenseCache(Path.Combine(AppPaths.Folder, "licensing"));
                client = new(paid, ReadKey("trial-public.pem"), uri, cache, DeviceIdentity.ReadFingerprint,
                    new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) });
            }
            configured = true; return client;
        }
    }
    public static void RequireBusinessUse()
    {
        var manager = Client;
        if (manager == null)
        {
#if DEBUG
            return; // Developer builds only. Never distribute them as licensed production clients.
#else
            throw new InvalidOperationException("This installer is not provisioned with a paid-license verification key. Ask the programmer. Reports/recovery remain accessible.");
#endif
        }
        try { manager.ValidateAsync().GetAwaiter().GetResult(); }
        catch (Exception ex) { throw new InvalidOperationException("Business posting requires a valid lifetime license or successful online trial validation. Data has not been erased.\n" + ex.Message, ex); }
    }
}
