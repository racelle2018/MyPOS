using System.Runtime.InteropServices;
namespace MyPos.Desktop.Printing;
internal static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private class DOCINFO { public string pDocName = "MyPos Receipt"; public string? pOutputFile; public string pDataType = "RAW"; }
    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool OpenPrinter(string name, out nint handle, nint defaults);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool ClosePrinter(nint handle);
    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)] private static extern int StartDocPrinter(nint handle, int level, [In] DOCINFO docInfo);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndDocPrinter(nint handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern int StartPagePrinter(nint handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndPagePrinter(nint handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool WritePrinter(nint handle, byte[] data, int count, out int written);
    public static void SendRaw(string printerName, byte[] bytes)
    {
        if (!OpenPrinter(printerName, out var handle, 0)) throw new InvalidOperationException($"Unable to open printer '{printerName}'.");
        try { var doc = new DOCINFO(); if (StartDocPrinter(handle, 1, doc) == 0) throw new InvalidOperationException("Unable to start the printer job."); try { if (StartPagePrinter(handle) == 0 || !WritePrinter(handle, bytes, bytes.Length, out var written) || written != bytes.Length) throw new InvalidOperationException("Printer rejected the receipt data."); EndPagePrinter(handle); } finally { EndDocPrinter(handle); } } finally { ClosePrinter(handle); }
    }
}
