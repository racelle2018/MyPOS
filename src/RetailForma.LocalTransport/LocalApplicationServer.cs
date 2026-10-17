using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using RetailForma.ServiceApplication;

namespace RetailForma.LocalTransport;

public sealed class LocalApplicationServer(string pipeName, SecurityIdentifier serviceSid,
    Func<ApplicationSession, bool, Operation, JsonElement, object?> execute, Action<string>? logFailure = null)
{
    private readonly SemaphoreSlim slots = new(16);
    private readonly SemaphoreSlim databaseGate = new(1); // serialize commands/role checks and postings on one local DB
    private readonly HashSet<Task> connections = [];

    public async Task RunAsync(CancellationToken stopping)
    {
        var first = true;
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                await slots.WaitAsync(stopping).ConfigureAwait(false);
                NamedPipeServerStream? pipe = null;
                try
                {
                    pipe = LocalPipeSecurity.Listen(pipeName, serviceSid, first); first = false;
                    await pipe.WaitForConnectionAsync(stopping).ConfigureAwait(false);
                    var clientPipe = pipe;
                    var task = Task.Run(() => ServeAsync(clientPipe, stopping), CancellationToken.None);
                    lock (connections) { connections.RemoveWhere(t => t.IsCompleted); connections.Add(task); }
                    pipe = null;
                }
                finally { if (pipe != null) { pipe.Dispose(); slots.Release(); } }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        finally
        {
            Task[] pending; lock (connections) pending = connections.ToArray();
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
    }
    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken stopping)
    {
        ApplicationSession? session = null;
        var elevatedMaintenance = false;
        try
        {
            while (!stopping.IsCancellationRequested && pipe.IsConnected)
            {
                // RunAsClient requires the first read before the OS token is available.
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(stopping);
                idle.CancelAfter(session?.IsAuthenticated == true ? TimeSpan.FromMinutes(35) :
                    elevatedMaintenance ? TimeSpan.FromMinutes(6) : TimeSpan.FromSeconds(15));
                var request = await PipeWire.ReadAsync<WireRequest>(pipe, PipeWire.RequestLimit, idle.Token).ConfigureAwait(false);
                var identity = LocalPipeSecurity.Identify(pipe);
                elevatedMaintenance = identity.Elevated;
                session ??= new ApplicationSession(identity.Sid);
                if (session.WindowsSid != identity.Sid) throw new UnauthorizedAccessException("Windows identity changed on the connection.");
                WireResponse response;
                await databaseGate.WaitAsync(stopping).ConfigureAwait(false);
                try
                {
                    try
                    {
                        var result = execute(session, identity.Elevated, request.Operation, request.Payload);
                        response = new(true, result == null ? null : JsonSerializer.SerializeToElement(result, result.GetType(), WireJson.Options));
                    }
                    catch (UnauthorizedAccessException ex) { response = new(false, Error: ex.Message, Code: "Access"); }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or InvalidDataException or JsonException)
                    { response = new(false, Error: ex.Message, Code: "Validation"); }
                    catch (Exception ex)
                    {
                        // Exception messages/SQL/payloads may contain credentials. Record only controlled
                        // operation/type/stack metadata; diagnostics failure must not leak or crash the pipe.
                        try { logFailure?.Invoke(request.Operation + ": " + ex.GetType().FullName + Environment.NewLine + ex.StackTrace); } catch { }
                        response = new(false, Error: "The service could not confirm the result. Verify the operation before retrying; it may already have completed.", Code: "Failure");
                    }
                }
                finally { databaseGate.Release(); }
                await PipeWire.WriteAsync(pipe, response, PipeWire.ResponseLimit, stopping).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException or UnauthorizedAccessException or JsonException) { }
        finally { session?.Clear(); pipe.Dispose(); slots.Release(); }
    }
}
