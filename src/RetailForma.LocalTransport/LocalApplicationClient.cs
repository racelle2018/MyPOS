using System.IO.Pipes;
using System.Text.Json;
using RetailForma.ServiceApplication;

namespace RetailForma.LocalTransport;

/// <summary>One OS-authenticated connection/session. Reconnection never silently signs a user in again.</summary>
public sealed class LocalApplicationClient : IDisposable
{
    private readonly object gate = new();
    private NamedPipeClientStream? pipe;
    public T Call<T>(Operation operation, object? input = null)
    {
        var value = Call(operation, input);
        return value.HasValue ? WireJson.Read<T>(value.Value) : default!;
    }
    public JsonElement? Call(Operation operation, object? input = null)
    {
        lock (gate)
        {
            pipe ??= LocalPipeSecurity.Connect(PipeWire.PipeName, LocalPipeSecurity.ServiceSid);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                PipeWire.WriteAsync(pipe, new WireRequest(operation, WireJson.Element(input ?? new { })), PipeWire.RequestLimit, timeout.Token).GetAwaiter().GetResult();
                var response = PipeWire.ReadAsync<WireResponse>(pipe, PipeWire.ResponseLimit, timeout.Token).GetAwaiter().GetResult();
                if (!response.Success)
                {
                    if (response.Code == "Access") throw new UnauthorizedAccessException(response.Error ?? "Access denied.");
                    throw new InvalidOperationException(response.Error ?? "Operation not completed.");
                }
                return response.Value;
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException)
            { pipe.Dispose(); pipe = null; throw new IOException("Service connection lost. Reopen and sign in; check the result before retrying a transaction.", ex); }
        }
    }
    public void Dispose() { lock (gate) { pipe?.Dispose(); pipe = null; } }
}
