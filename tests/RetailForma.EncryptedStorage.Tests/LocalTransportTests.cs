using System.Buffers.Binary;
using System.Security.Principal;
using RetailForma.ServiceApplication;
using RetailForma.LocalTransport;

namespace RetailForma.EncryptedStorage.Tests;

public sealed class LocalTransportTests
{
    [Fact]
    public async Task ActualWindowsPeerIsIdentifiedAndMessagesAreBounded()
    {
        using var windows = WindowsIdentity.GetCurrent();
        var sid = windows.User!; var name = "RetailForma-test-pipe-" + Guid.NewGuid().ToString("N");
        using var stopping = new CancellationTokenSource();
        var calls = 0;
        var server = new LocalApplicationServer(name, sid, (session, elevated, operation, payload) =>
        { Interlocked.Increment(ref calls); return new { Peer = session.WindowsSid, Operation = (int)operation }; });
        var running = server.RunAsync(stopping.Token);
        try
        {
            using (var client = LocalPipeSecurity.Connect(name, sid))
            {
                await PipeWire.WriteAsync(client, new WireRequest(Operation.PublicIdentity, WireJson.Element(new { })), PipeWire.RequestLimit, CancellationToken.None);
                var response = await PipeWire.ReadAsync<WireResponse>(client, PipeWire.ResponseLimit, CancellationToken.None);
                Assert.True(response.Success); Assert.Equal(sid.Value, response.Value!.Value.GetProperty("Peer").GetString());
            }
            using (var invalid = LocalPipeSecurity.Connect(name, sid))
            {
                var prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, PipeWire.RequestLimit + 1);
                await invalid.WriteAsync(prefix); await invalid.FlushAsync();
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var buffer = new byte[1];
                Assert.Equal(0, await invalid.ReadAsync(buffer, deadline.Token));
            }
            Assert.Equal(1, calls);
            using (var healthy = LocalPipeSecurity.Connect(name, sid))
            {
                await PipeWire.WriteAsync(healthy, new WireRequest(Operation.PublicIdentity, WireJson.Element(new { })), PipeWire.RequestLimit, CancellationToken.None);
                Assert.True((await PipeWire.ReadAsync<WireResponse>(healthy, PipeWire.ResponseLimit, CancellationToken.None)).Success);
            }
            Assert.Equal(2, calls);
        }
        finally { stopping.Cancel(); await running.WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Fact]
    public async Task ClientRejectsAnUntrustedPipeOwnerBeforeSendingCredentials()
    {
        using var windows = WindowsIdentity.GetCurrent(); var sid = windows.User!;
        var name = "RetailForma-test-impostor-" + Guid.NewGuid().ToString("N");
        using var stopping = new CancellationTokenSource(); var called = false;
        var server = new LocalApplicationServer(name, sid, (_, _, _, _) => { called = true; return null; });
        var running = server.RunAsync(stopping.Token);
        try
        { Assert.Throws<UnauthorizedAccessException>(() => LocalPipeSecurity.Connect(name, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null))); }
        finally { stopping.Cancel(); await running.WaitAsync(TimeSpan.FromSeconds(10)); }
        Assert.False(called);
    }

    [Fact]
    public void ClientRightsCannotCreateAdditionalServerInstancesAndNetworkLogonsAreDenied()
    {
        Assert.Equal((System.IO.Pipes.PipeAccessRights)0, LocalPipeSecurity.ClientRights & System.IO.Pipes.PipeAccessRights.CreateNewInstance);
        using var identity = WindowsIdentity.GetCurrent(); var descriptor = LocalPipeSecurity.Descriptor(identity.User!);
        var rules = descriptor.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<System.IO.Pipes.PipeAccessRule>().ToArray();
        Assert.Contains(rules, r => r.IdentityReference.Value == "S-1-5-2" && r.AccessControlType == System.Security.AccessControl.AccessControlType.Deny);
    }
}
