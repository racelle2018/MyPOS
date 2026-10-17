using System.Management;
using Microsoft.Win32;
using RetailForma.Core.Services;
namespace RetailForma.Desktop.Licensing;
public static class DeviceIdentity
{
    public static string ReadFingerprint()
    {
        static string Read(string query, string property)
        {
            using var search = new ManagementObjectSearcher(query);
            search.Options.Timeout = TimeSpan.FromSeconds(3);
            using var collection = search.Get();
            var values = new List<string>();
            foreach (ManagementObject item in collection)
                using (item) if (item[property]?.ToString() is { Length: > 0 } value) values.Add(value.Trim());
            return values.Order(StringComparer.Ordinal).FirstOrDefault() ?? "";
        }
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var cryptography = machine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        return LicenseProtocol.Fingerprint(Read("SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber"),
            Read("SELECT ProcessorId FROM Win32_Processor", "ProcessorId"), cryptography?.GetValue("MachineGuid")?.ToString() ?? "");
    }
}
