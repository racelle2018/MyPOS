using System.Buffers.Binary;
using System.Text.Json;
using RetailForma.ServiceApplication;

namespace RetailForma.LocalTransport;

public static class PipeWire
{
    public const string PipeName = "RetailForma.Data.v1";
    public const int RequestLimit = 1024 * 1024, ResponseLimit = 32 * 1024 * 1024;

    public static async Task<T> ReadAsync<T>(Stream stream, int limit, CancellationToken cancellationToken)
    {
        var size = new byte[4];
        await stream.ReadExactlyAsync(size, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(size);
        if (length <= 0 || length > limit) throw new InvalidDataException("Invalid or oversized local-service message.");
        var bytes = new byte[length];
        try
        {
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(bytes, WireJson.Options) ?? throw new InvalidDataException("Missing local-service message.");
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }
    public static async Task WriteAsync<T>(Stream stream, T value, int limit, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, WireJson.Options);
        try
        {
            if (bytes.Length <= 0 || bytes.Length > limit) throw new InvalidDataException("The response is too large. Narrow the selected scope.");
            var size = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(size, bytes.Length);
            await stream.WriteAsync(size, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }
}
