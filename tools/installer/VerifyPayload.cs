#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property OutputType=Exe
#:property PublishAot=false
#:property PublishTrimmed=false

using System.Collections;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;

if (args.Length != 4) throw new ArgumentException("Published DLL, paid PUBLIC PEM, trial PUBLIC PEM or '-', endpoint or '-' required.");
static byte[] PublicKey(string pem)
{
    if (pem.Contains("PRIVATE KEY") || !pem.Contains("-----BEGIN PUBLIC KEY-----")) throw new InvalidDataException("Only SPKI public keys are permitted.");
    using var rsa = RSA.Create(); rsa.ImportFromPem(pem);
    if (rsa.KeySize < 3072) throw new InvalidDataException("RSA-3072 or stronger required.");
    return rsa.ExportSubjectPublicKeyInfo();
}
var paid = PublicKey(File.ReadAllText(args[1]));
var trial = args[2] == "-" ? null : PublicKey(File.ReadAllText(args[2]));
if ((trial == null) != (args[3] == "-")) throw new InvalidDataException("Trial trust and endpoint must be supplied together.");
if (trial != null && CryptographicOperations.FixedTimeEquals(paid, trial)) throw new InvalidDataException("Paid and trial keys must be distinct.");
if (trial != null && (!Uri.TryCreate(args[3], UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https")) throw new InvalidDataException("Trial HTTPS endpoint required.");
var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
if (assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration != "Release") throw new InvalidDataException("Release build required.");
var embedded = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
foreach (var name in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".g.resources", StringComparison.Ordinal)))
{
    using var stream = assembly.GetManifestResourceStream(name)!;
    using var resources = new ResourceReader(stream);
    foreach (DictionaryEntry entry in resources)
    {
        var key = (string)entry.Key;
        if (key is not ("licensing/paid-public.pem" or "licensing/trial-public.pem")) continue;
        if (entry.Value is not Stream keyStream) throw new InvalidDataException("Unexpected public-resource format.");
        using var reader = new StreamReader(keyStream); embedded.Add(key, PublicKey(reader.ReadToEnd()));
    }
}
if (!embedded.TryGetValue("licensing/paid-public.pem", out var actualPaid) || !CryptographicOperations.FixedTimeEquals(paid, actualPaid)) throw new InvalidDataException("Embedded paid trust mismatch.");
if (trial == null && embedded.ContainsKey("licensing/trial-public.pem")) throw new InvalidDataException("Unexpected trial trust.");
if (trial != null && (!embedded.TryGetValue("licensing/trial-public.pem", out var actualTrial) || !CryptographicOperations.FixedTimeEquals(trial, actualTrial))) throw new InvalidDataException("Embedded trial trust mismatch.");
var actualEndpoint = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "RetailFormaTrialEndpoint").Value ?? "";
if (actualEndpoint != (args[3] == "-" ? "" : args[3])) throw new InvalidDataException("Embedded trial endpoint mismatch.");
Console.WriteLine("Verified Release payload and matching RSA public trust; POS startup not invoked.");
