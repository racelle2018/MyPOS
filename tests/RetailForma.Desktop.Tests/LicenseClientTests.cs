using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using RetailForma.Core.Services;
using RetailForma.Desktop.Licensing;
using Xunit;
namespace RetailForma.Desktop.Tests;
public sealed class LicenseClientTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "retailforma-license-client-" + Guid.NewGuid().ToString("N"));
    private readonly RSA paid = RSA.Create(3072), trial = RSA.Create(3072);
    private readonly string fingerprint = new('A', 64);
    private readonly DateTimeOffset start = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
    public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); paid.Dispose(); trial.Dispose(); }
    private LicenseClaims Claims(LicenseKind kind) => new(1, Guid.NewGuid(), fingerprint, kind, start, kind == LicenseKind.Trial ? start.AddDays(7) : null, ["Basic"]);
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; return response(request); }
    }
    [Fact]
    public async Task Lifetime_import_and_repeated_validation_never_call_internet_and_cache_is_encrypted()
    {
        using var handler = new Handler(_ => throw new HttpRequestException("No internet fixture"));
        using var http = new HttpClient(handler);
        var cache = new LicenseCache(folder);
        var client = new LicenseClient(paid.ExportSubjectPublicKeyInfoPem(), trial.ExportSubjectPublicKeyInfoPem(), new Uri("https://test.invalid/api/trial-validate"), cache, () => fingerprint, http);
        var signed = LicenseProtocol.Sign(Claims(LicenseKind.Lifetime), paid);
        await client.ImportAsync(signed); await client.ValidateAsync(); await client.ValidateAsync();
        Assert.Equal(0, handler.Calls); Assert.Equal(signed, cache.Load());
        Assert.DoesNotContain(signed.Payload, System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(folder, "license.aes"))));
    }
    [Fact]
    public async Task Trial_uses_fresh_signed_server_utc_and_offline_or_expired_response_never_grants_use()
    {
        var claims = Claims(LicenseKind.Trial); var online = true; var serverUtc = start.AddDays(6); var challenges = new HashSet<string>();
        using var handler = new Handler(async request =>
        {
            if (!online) throw new HttpRequestException("Offline fixture");
            using var data = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var challenge = data.RootElement.GetProperty("Challenge").GetString()!; Assert.True(challenges.Add(challenge));
            var validation = LicenseProtocol.Sign(new TrialValidation(1, claims.LicenseId, fingerprint, challenge, serverUtc, true), trial);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(validation, options: new JsonSerializerOptions { PropertyNamingPolicy = null }) };
        });
        using var http = new HttpClient(handler);
        var client = new LicenseClient(paid.ExportSubjectPublicKeyInfoPem(), trial.ExportSubjectPublicKeyInfoPem(), new Uri("https://test.invalid/api/trial-validate"), new LicenseCache(folder), () => fingerprint, http);
        await client.ImportAsync(LicenseProtocol.Sign(claims, trial)); await client.ValidateAsync();
        online = false; await Assert.ThrowsAsync<HttpRequestException>(() => client.ValidateAsync());
        online = true; serverUtc = start.AddDays(7); await Assert.ThrowsAsync<InvalidOperationException>(() => client.ValidateAsync());
        Assert.True(handler.Calls >= 4);
    }
    [Fact]
    public async Task Trial_signing_key_cannot_forge_paid_license_and_rejected_import_preserves_active_cache()
    {
        using var http = new HttpClient(new Handler(_ => throw new Exception("No network expected")));
        var cache = new LicenseCache(folder);
        var client = new LicenseClient(paid.ExportSubjectPublicKeyInfoPem(), trial.ExportSubjectPublicKeyInfoPem(), null, cache, () => fingerprint, http);
        var original = LicenseProtocol.Sign(Claims(LicenseKind.Lifetime), paid); await client.ImportAsync(original);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.ImportAsync(LicenseProtocol.Sign(Claims(LicenseKind.Lifetime), trial)));
        Assert.Equal(original, cache.Load());
    }
    [Fact]
    public async Task Trial_invitation_validates_server_proof_before_storage()
    {
        var claims = Claims(LicenseKind.Trial); var cache = new LicenseCache(folder);
        using var handler = new Handler(async request =>
        {
            Assert.Equal("/api/trial-start", request.RequestUri!.AbsolutePath);
            using var data = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("fixture-invitation", data.RootElement.GetProperty("ActivationKey").GetString());
            var challenge = data.RootElement.GetProperty("Challenge").GetString()!;
            var proof = LicenseProtocol.Sign(new TrialValidation(1, claims.LicenseId, fingerprint, challenge, start, true), trial);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { License = LicenseProtocol.Sign(claims, trial), Validation = proof },
                options: new JsonSerializerOptions { PropertyNamingPolicy = null }) };
        });
        using var http = new HttpClient(handler);
        var client = new LicenseClient(paid.ExportSubjectPublicKeyInfoPem(), trial.ExportSubjectPublicKeyInfoPem(), new Uri("https://test.invalid/api/trial-validate"), cache, () => fingerprint, http);
        Assert.Equal(claims.LicenseId, (await client.StartTrialAsync(claims.LicenseId, "fixture-invitation")).LicenseId);
        Assert.True(cache.Exists); Assert.Equal(1, handler.Calls);
    }
}
