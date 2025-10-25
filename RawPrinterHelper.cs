using System;
using System.Runtime.InteropServices;

public static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOC_INFO_1
    {
        public string pDocName;
        public string? pOutputFile;
        public string pDatatype;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    // Firma correcta para pasar DOC_INFO_1 por referencia (Level = 1)
    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int Level, [In] ref DOC_INFO_1 pDocInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    // Mantener por compatibilidad; corregido y redirigido a SendRawJob
    public static void SendBytes(string printerName, byte[] bytes)
    {
        SendRawJob(printerName, bytes, "RAW Job");
    }

    public static void SendRawJob(string printerName, byte[] data, string docName = "RAW Job")
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new ArgumentException("Printer name is required", nameof(printerName));
        if (data is null || data.Length == 0)
            throw new ArgumentException("Data is empty", nameof(data));

        if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
            ThrowLastPInvokeError("OpenPrinter failed");

        var pageStarted = false;
        var docStarted = false;
        IntPtr unmanagedBuffer = IntPtr.Zero;

        try
        {
            var di = new DOC_INFO_1
            {
                pDocName = docName,
                pOutputFile = null,
                pDatatype = "RAW"
            };

            if (!StartDocPrinter(hPrinter, 1, ref di))
                ThrowLastPInvokeError("StartDocPrinter failed");
            docStarted = true;

            if (!StartPagePrinter(hPrinter))
                ThrowLastPInvokeError("StartPagePrinter failed");
            pageStarted = true;

            unmanagedBuffer = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, unmanagedBuffer, data.Length);

            if (!WritePrinter(hPrinter, unmanagedBuffer, data.Length, out var written) || written != data.Length)
                ThrowLastPInvokeError($"WritePrinter failed (written {written} of {data.Length})");
        }
        finally
        {
            if (unmanagedBuffer != IntPtr.Zero)
                Marshal.FreeHGlobal(unmanagedBuffer);

            if (pageStarted)
                EndPagePrinter(hPrinter);

            if (docStarted)
                EndDocPrinter(hPrinter);

            ClosePrinter(hPrinter);
        }
    }

    private static void ThrowLastPInvokeError(string message)
    {
        // En .NET 6+ es más fiable que GetLastWin32Error cuando se usa DllImport
        int error = Marshal.GetLastPInvokeError();
        throw new InvalidOperationException($"{message}. Win32Error={error}");
    }
}