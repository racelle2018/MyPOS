using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using RetailForma.Core.Services;

namespace RetailForma.Desktop.Licensing;

public sealed class LicenseClient
{
    private static readonly JsonSerializerOptions WireJson = new() { PropertyNamingPolicy = null };
    private readonly string paidPublicKey;
    private readonly string? trialPublicKey;
    private readonly Uri? trialEndpoint;
    private readonly LicenseCache cache;
    private readonly Lazy<string> fingerprint;
    private readonly HttpClient http;
    public LicenseClient(string paidPublicKey, string? trialPublicKey, Uri? trialEndpoint, LicenseCache cache,
        Func<string> readFingerprint, HttpClient http)
    {
        if (trialEndpoint != null && (trialEndpoint.Scheme != Uri.UriSchemeHttps || trialEndpoint.UserInfo.Length > 0))
            throw new ArgumentException("Trial validation requires an HTTPS endpoint without embedded credentials.");
        using var paidKey = RSA.Create();
        if (paidPublicKey.Contains("PRIVATE KEY", StringComparison.Ordinal)) throw new ArgumentException("Client requires public keys only.");
        paidKey.ImportFromPem(paidPublicKey);
        if (paidKey.KeySize < 3072) throw new ArgumentException("Use RSA-3072 or stronger public keys.");
        if (!string.IsNullOrWhiteSpace(trialPublicKey))
        {
            using var trialKey = RSA.Create();
            if (trialPublicKey.Contains("PRIVATE KEY", StringComparison.Ordinal)) throw new ArgumentException("Never install the trial private key in the client.");
            trialKey.ImportFromPem(trialPublicKey);
            if (trialKey.KeySize < 3072 || CryptographicOperations.FixedTimeEquals(paidKey.ExportSubjectPublicKeyInfo(), trialKey.ExportSubjectPublicKeyInfo()))
                throw new ArgumentException("Paid and online trial verification keys must be separate RSA-3072+ keys.");
        }
        this.paidPublicKey = paidPublicKey; this.trialPublicKey = trialPublicKey; this.trialEndpoint = trialEndpoint;
        this.cache = cache; this.http = http; fingerprint = new(readFingerprint);
    }
    public LicenseRequest Request(string company) => new(1, company, fingerprint.Value);
    private RSA KeyFor(SignedLicenseMessage token)
    {
        // Untrusted Kind chooses a fixed publisher key; no grant occurs before signature validation.
        if (token.Payload.Length > 24000) throw new InvalidDataException("License payload is too large.");
        using var json = JsonDocument.Parse(Convert.FromBase64String(token.Payload));
        var kind = (LicenseKind)json.RootElement.GetProperty("Kind").GetInt32();
        var pem = kind == LicenseKind.Trial ? trialPublicKey : paidPublicKey;
        if (string.IsNullOrWhiteSpace(pem) || pem.Contains("PRIVATE KEY", StringComparison.Ordinal))
            throw new InvalidOperationException("The installer has no valid public verification key for this license type.");
        var key = RSA.Create();
        try { key.ImportFromPem(pem); return key; }
        catch { key.Dispose(); throw; }
    }
    public Task<LicenseClaims> ValidateAsync(CancellationToken cancellationToken = default) => ValidateTokenAsync(cache.Load(), cancellationToken);
    private sealed record TrialStartResponse(SignedLicenseMessage License, SignedLicenseMessage Validation);
    public async Task<LicenseClaims> StartTrialAsync(Guid licenseId, string activationKey, CancellationToken cancellationToken = default)
    {
        if (trialEndpoint == null) throw new InvalidOperationException("Trial server is not configured.");
        var challenge = LicenseProtocol.NewChallenge();
        var result = await PostAsync<TrialStartResponse>(new Uri(trialEndpoint, "trial-start"),
            new { LicenseId = licenseId, ActivationKey = activationKey, Fingerprint = fingerprint.Value, Challenge = challenge }, cancellationToken).ConfigureAwait(false);
        using var key = KeyFor(result.License);
        var claims = LicenseProtocol.ValidateTrialOnline(result.License, result.Validation, key, fingerprint.Value, challenge);
        PreserveLifetime(claims);
        cache.SaveVerified(result.License, key, fingerprint.Value);
        return claims;
    }
    private void PreserveLifetime(LicenseClaims incoming)
    {
        if (incoming.Kind != LicenseKind.Trial || !cache.Exists) return;
        var current = cache.Load(); using var key = KeyFor(current);
        if (LicenseProtocol.VerifyLicense(current, key, fingerprint.Value).Kind == LicenseKind.Lifetime)
            throw new InvalidOperationException("A trial cannot replace an installed lifetime license.");
    }
    public async Task<LicenseClaims> ImportAsync(SignedLicenseMessage token, CancellationToken cancellationToken = default)
    {
        var claims = await ValidateTokenAsync(token, cancellationToken).ConfigureAwait(false);
        PreserveLifetime(claims);
        using var key = KeyFor(token);
        cache.SaveVerified(token, key, fingerprint.Value);
        return claims;
    }
    private async Task<LicenseClaims> ValidateTokenAsync(SignedLicenseMessage token, CancellationToken cancellationToken)
    {
        using var key = KeyFor(token);
        var claims = LicenseProtocol.VerifyLicense(token, key, fingerprint.Value);
        if (claims.Kind == LicenseKind.Lifetime) return claims; // No network/current-time dependency.
        if (trialEndpoint == null) throw new InvalidOperationException("Online trial validation is not configured. Ask the programmer; trial use remains unavailable.");
        var challenge = LicenseProtocol.NewChallenge();
        var signedResponse = await PostAsync<SignedLicenseMessage>(trialEndpoint, new { License = token, Challenge = challenge }, cancellationToken).ConfigureAwait(false);
        return LicenseProtocol.ValidateTrialOnline(token, signedResponse, key, fingerprint.Value, challenge);
    }
    private async Task<T> PostAsync<T>(Uri endpoint, object payload, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5)); cancellationToken = timeout.Token;
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        { Content = JsonContent.Create(payload, options: WireJson) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != System.Net.HttpStatusCode.OK)
            throw new InvalidOperationException("Trial validation unavailable or denied. This is not proof that the trial has expired.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var body = new MemoryStream(); var buffer = new byte[4096]; int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (body.Length + count > 16384) throw new InvalidDataException("Trial response exceeds the permitted size.");
            body.Write(buffer, 0, count);
        }
        return JsonSerializer.Deserialize<T>(body.ToArray()) ?? throw new InvalidDataException("Missing signed trial response.");
    }
}
