using Microsoft.AspNetCore.Mvc;
using PrintSpoolJobService.Models;
using Spire.Pdf;
using Spire.Pdf.Print;
using System.Drawing.Printing;
using System.Globalization;
using System.Net;
using System.Diagnostics;
using System.Text;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace PrintSpoolJobService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]    
    public class PrinterController : ControllerBase
    {
        private readonly ILogger<PrinterController>? _logger;
        public PrinterController(ILogger<PrinterController> logger)
        {
            _logger = logger;
        }
        [HttpGet("get-printers")]
        public IActionResult GetPrinters()
        {
            try
            {
                // PrinterSettings.InstalledPrinters is only supported on Windows (6.1+).
                // On non-Windows platforms try to enumerate via CUPS (`lpstat`) if available.
                string[] printers;
                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        printers = GetCupsPrinters();
                        if (printers.Length == 0)
                        {
                            _logger?.LogWarning("Printer enumeration not available on non-Windows platform or no printers found");
                            return StatusCode(501, new {
                                message = "Printer enumeration not implemented on this platform",
                                reason = "CUPS (lpstat) not available or no printers configured",
                                resolution = "Install CUPS (provides lpstat/lpoptions) or enable the CUPS web interface at http://localhost:631"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to enumerate printers via CUPS on non-Windows platform");
                        return StatusCode(501, new {
                            message = "Printer enumeration not implemented on this platform",
                            reason = "Error executing CUPS utilities or accessing CUPS web interface",
                            details = ex.Message,
                            resolution = "Ensure CUPS is installed and lpstat/lpoptions are available, or that the CUPS web UI is accessible on http://localhost:631"
                        });
                    }
                }
                else
                {
                    printers = PrinterSettings
                    .InstalledPrinters
                    .Cast<string>()
                    .OrderBy(p => p)
                    .ToArray();

                    if (printers.Length == 0)
                    {
                        _logger?.LogWarning("No printers found");
                    }
                }

                // 200 con lista (posiblemente vacía) es más predecible para el cliente
                return Ok(printers);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching printer list");
                return StatusCode(500, "Internal server error - printers");
            }
        }

        [HttpGet("get-local-ipaddress")]
        public IActionResult GetLocalIPAddress([FromQuery] string? select = "all")
        {
            try
            {
                // 1) IP local del socket HTTP (útil detrás de reverse proxy o múltiples NICs)
                var httpLocal = HttpContext?.Connection?.LocalIpAddress;

                // 2) Recoger direcciones de NICs operativas y no virtuales/túnel
                var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni =>
                        ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel);

                var unicast = nics
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses.Select(ua => new { ni, ua.Address }))
                    .ToArray();

                static bool IsApipa(IPAddress ip)
                {
                    if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
                    var b = ip.GetAddressBytes();
                    return b.Length >= 2 && b[0] == 169 && b[1] == 254;
                }

                // IPv4 válidas
                var v4 = unicast
                    .Select(x => x.Address)
                    .Where(ip =>
                        ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ip) &&
                        !IsApipa(ip) &&
                        !ip.Equals(IPAddress.Any))
                    .Distinct()
                    .ToList();

                // IPv6 válidas (evitar link-local, Teredo, multicast, unspecified)
                var v6 = unicast
                    .Select(x => x.Address)
                    .Where(ip =>
                        ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
                        !ip.IsIPv6LinkLocal &&
                        !ip.IsIPv6Teredo &&
                        !ip.IsIPv6Multicast &&
                        !ip.Equals(IPAddress.IPv6Any))
                    .Distinct()
                    .ToList();

                // Heurística simple para una "primaria": prioriza direcciones con puerta de enlace
                string? PrimaryOf(IEnumerable<IPAddress> addresses, System.Net.Sockets.AddressFamily af)
                {
                    var nicWithGw = nics.FirstOrDefault(ni =>
                        ni.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == af));
                    if (nicWithGw != null)
                    {
                        var addr = nicWithGw.GetIPProperties().UnicastAddresses
                            .Select(ua => ua.Address)
                            .FirstOrDefault(ip => addresses.Contains(ip));
                        if (addr != null) return addr.ToString();
                    }
                    return addresses.FirstOrDefault()?.ToString();
                }

                var primaryV4 = PrimaryOf(v4, System.Net.Sockets.AddressFamily.InterNetwork);
                var primaryV6 = PrimaryOf(v6, System.Net.Sockets.AddressFamily.InterNetworkV6);

                var resultAll = new
                {
                    hostname = Dns.GetHostName(),
                    httpLocal = httpLocal?.ToString(),
                    ipv4 = v4.Select(a => a.ToString()).ToArray(),
                    ipv6 = v6.Select(a => a.ToString()).ToArray(),
                    primaryV4,
                    primaryV6
                };

                var sel = (select ?? "all").Trim().ToLowerInvariant();
                object response = resultAll;

                switch (sel)
                {
                    case "ipv4":
                        if (v4.Count == 0) return NotFound("No se encontraron direcciones IPv4 válidas");
                        response = new
                        {
                            hostname = resultAll.hostname,
                            httpLocal = resultAll.httpLocal,
                            ipv4 = resultAll.ipv4,
                            primaryV4 = resultAll.primaryV4
                        };
                        break;

                    case "ipv6":
                        if (v6.Count == 0) return NotFound("No se encontraron direcciones IPv6 válidas");
                        response = new
                        {
                            hostname = resultAll.hostname,
                            httpLocal = resultAll.httpLocal,
                            ipv6 = resultAll.ipv6,
                            primaryV6 = resultAll.primaryV6
                        };
                        break;

                    case "primary":
                        if (primaryV4 is null && primaryV6 is null && httpLocal is null)
                            return NotFound("No se encontró dirección IP primaria");
                        response = new
                        {
                            hostname = resultAll.hostname,
                            httpLocal = resultAll.httpLocal,
                            primaryV4 = resultAll.primaryV4,
                            primaryV6 = resultAll.primaryV6
                        };
                        break;

                    case "httplocal":
                        if (httpLocal is null) return NotFound("No se encontró IP local del socket HTTP");
                        response = new
                        {
                            hostname = resultAll.hostname,
                            httpLocal = resultAll.httpLocal
                        };
                        break;

                    case "all":
                    default:
                        if (v4.Count == 0 && v6.Count == 0 && httpLocal is null)
                            return NotFound("No se encontraron direcciones IP locales válidas");
                        response = resultAll;
                        break;
                }

                _logger?.LogInformation("IPs locales detectadas. IPv4={CountV4}, IPv6={CountV6}, select={Select}", v4.Count, v6.Count, sel);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error obteniendo direcciones IP locales");
                return StatusCode(500, "Error interno del servidor - IP locales");
            }
        }

        // Imprimir PDFs
        [HttpPost("print-pdf")]
        [RequestSizeLimit(10_000_000)] // 10 MB
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PrintPDF(IFormFile documentPDF, [FromForm] string printerName, CancellationToken ct)
        {
            if (documentPDF is null || documentPDF.Length == 0)
                return BadRequest("PDF document cannot be null or empty");

            if (!IsPdfContentType(documentPDF.ContentType))
                return BadRequest("Content-Type must be 'application/pdf'");

            if (!IsPrinterNameSafe(printerName))
                return BadRequest("Printer name contains invalid characters");

            if (!PrinterExists(printerName))
                return NotFound($"Printer '{printerName}' not found");

            return await PrintPdfInternalAsync(documentPDF, printerName.Trim(), ct);
        }

        // Imprimir EZPL/ZPL como RAW a impresoras tipo Zebra
        [HttpPost("print-label")]
        [RequestSizeLimit(2_000_000)] // 2 MB
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PrintLabel(IFormFile documentEZPL, [FromForm] string printerName, CancellationToken ct)
        {
            if (documentEZPL is null || documentEZPL.Length == 0)
                return BadRequest("EZPL Document cannot be null or empty");

            if (!IsLabelContentType(documentEZPL.ContentType))
                return BadRequest("Content-Type must be 'text/plain' or 'application/octet-stream'");

            if (!IsPrinterNameSafe(printerName))
                return BadRequest("Printer name contains invalid characters");

            var targetPrinter = printerName.Trim();
            if (!PrinterExists(targetPrinter))
                return NotFound($"Printer '{targetPrinter}' not found");

            try
            {
                await using var input = documentEZPL.OpenReadStream();
                using var ms = new MemoryStream(capacity: (int)documentEZPL.Length);
                await input.CopyToAsync(ms, ct);

                var bytes = ms.ToArray();

                // Si viene como text/plain y el contenido comienza con BOM UTF-8, eliminarlo.
                if (string.Equals(documentEZPL.ContentType, "text/plain", StringComparison.OrdinalIgnoreCase) &&
                    bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    bytes = bytes[3..];
                }

                ct.ThrowIfCancellationRequested();

                RawPrinterHelper.SendRawJob(targetPrinter, bytes, "Label RAW Job");

                _logger?.LogInformation(
                    "Label job sent to printer {Printer}. Size={Size} bytes, ContentType={ContentType}",
                    targetPrinter, bytes.Length, documentEZPL.ContentType);

                return Ok("EZPL document printed successfully");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Print label canceled by client for printer {Printer}", targetPrinter);
                return StatusCode(499, "Client Closed Request");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error printing EZPL Document to {Printer}", targetPrinter);
                return StatusCode(500, "Internal server error - Error print EZPL Document");
            }
        }
        
        // Print POS Ticket - Thermal Printer
        [HttpPost("print-ticket")]
        [RequestSizeLimit(2_000_000)] // 2 MB
        [Consumes("application/json")]
        public async Task<IActionResult> PrintTicketAsync([FromBody] Ticket root, [FromQuery] string printerName, CancellationToken ct)
        {
            if (!IsPrinterNameSafe(printerName))
                return BadRequest("Printer name contains invalid characters");

            var targetPrinter = printerName?.Trim() ?? string.Empty;
            if (!PrinterExists(targetPrinter))
                return NotFound($"Printer '{targetPrinter}' not found");

            try
            {
                ct.ThrowIfCancellationRequested();

                if (root is null)
                    return BadRequest("Invalid ticket JSON format");

                // Model-level validation
                var validationErrors = root.Validate().ToArray();
                if (validationErrors.Length > 0)
                {
                    _logger?.LogWarning("Ticket validation failed for printer {Printer}: {Errors}", targetPrinter, string.Join("; ", validationErrors));
                    return BadRequest(new { errors = validationErrors });
                }

                // Build printer-ready bytes from the Ticket model
                var bytes = root.ToPrinterBytes();

                ct.ThrowIfCancellationRequested();

                // Send raw job to printer (uses existing helper in the project)
                RawPrinterHelper.SendRawJob(targetPrinter, bytes, "Ticket RAW Job");

                _logger?.LogInformation("Ticket job sent to printer {Printer}. Size={Size} bytes, Items={Items}",
                    targetPrinter, bytes.Length, root.Items?.Count ?? 0);

                return Ok("Ticket printed successfully");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Print ticket canceled by client for printer {Printer}", targetPrinter);
                return StatusCode(499, "Client Closed Request");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error printing Ticket to {Printer}", targetPrinter);
                return StatusCode(500, "Internal server error - Error print Ticket");
            }
        }

        // ----------------- Helpers privados -----------------

        private async Task<IActionResult> PrintPdfInternalAsync(IFormFile pdfFile, string printerName, CancellationToken ct)
        {
            try
            {
                await using var input = pdfFile.OpenReadStream(); //Recuperar stream del archivo PDF obtenido
                using var buffered = new MemoryStream(); // asegura Seek y validación de cabecera
                await input.CopyToAsync(buffered, ct);

                if (!IsPdf(buffered))
                {
                    return BadRequest("The uploaded file is not a valid PDF");
                }

                buffered.Position = 0;

                using var pdf = new PdfDocument();
                pdf.LoadFromStream(buffered);

                // Verificación: documento sin páginas
                if (pdf.Pages.Count == 0)
                {
                    _logger?.LogWarning("Rejected PDF with zero pages for printer {Printer}", printerName);
                    return BadRequest("The PDF document has no pages");
                }

                // Verificación: páginas vacías (heurística basada en texto)
                int blankPages = 0;
                for (int i = 0; i < pdf.Pages.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var page = pdf.Pages[i];
                    string? text = page.ExtractText();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        blankPages++;
                    }
                }

                if (blankPages == pdf.Pages.Count)
                {
                    _logger?.LogWarning("Rejected PDF with only blank pages for printer {Printer}", printerName);
                    return BadRequest("The PDF document contains only blank pages");
                }
                else if (blankPages > 0)
                {
                    _logger?.LogInformation("PDF contains {Blank} blank pages of {Total}", blankPages, pdf.Pages.Count);
                }

                var settings = new PdfPrintSettings
                {
                    PrinterName = printerName.Trim()
                };
                settings.SelectPageRange(1, pdf.Pages.Count);
                settings.SelectSinglePageLayout(PdfSinglePageScalingMode.FitSize);

                _logger?.LogInformation("Printing PDF to {Printer}. Pages={Pages}, BlankPages={Blank}, Size={Size} bytes",
                    printerName, pdf.Pages.Count, blankPages, pdfFile.Length);

                ct.ThrowIfCancellationRequested();
                pdf.Print(settings);

                return Ok("PDF document printed successfully");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Print PDF canceled by client for printer {Printer}", printerName);
                return StatusCode(499, "Client Closed Request");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error printing PDF Document to {Printer}", printerName);
                return StatusCode(500, "Internal server error - Error print PDF Document");
            }
        }
        
        private static bool PrinterExists(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName)) return false;
            var target = printerName.Trim();
            if (OperatingSystem.IsWindows())
            {
                return PrinterSettings
                    .InstalledPrinters
                    .Cast<string>()
                    .Any(p => string.Equals(p, target, StringComparison.OrdinalIgnoreCase));
            }

            try
            {
                var cups = GetCupsPrinters();
                return cups.Any(p => string.Equals(p, target, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                // If enumeration via lpstat fails, be conservative and return false.
                return false;
            }
        }

        private static string[] GetCupsPrinters()
        {
            // Try command-line tools first (lpstat / lpoptions). If those fail, fallback to HTTP scraping
            // of the local CUPS web interface at http://localhost:631/printers/.

            try
            {
                var result = GetCupsPrintersViaLpstat();
                if (result.Length > 0) return result;
            }
            catch (Exception ex)
            {
                _ = ex; // swallow and fallback
            }

            try
            {
                var result = GetCupsPrintersViaHttp();
                return result;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string[] GetCupsPrintersViaLpstat()
        {
            // Use `lpstat -p -d` first. If lpstat is missing or returns empty, try combining
            // `lpstat -p` and `lpoptions -d` to detect default.
            string output = null!;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "lpstat",
                    Arguments = "-p -d",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return Array.Empty<string>();

                var outTask = proc.StandardOutput.ReadToEndAsync();
                var errTask = proc.StandardError.ReadToEndAsync();
                if (!proc.WaitForExit(2000))
                {
                    try { proc.Kill(true); } catch { }
                }

                output = outTask.IsCompleted ? outTask.Result : outTask.GetAwaiter().GetResult();
                var err = errTask.IsCompleted ? errTask.Result : errTask.GetAwaiter().GetResult();

                if (string.IsNullOrWhiteSpace(output))
                {
                    // lpstat returned nothing; try lpstat -p only and lpoptions -d for default
                    var psi2 = new ProcessStartInfo
                    {
                        FileName = "lpstat",
                        Arguments = "-p",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc2 = Process.Start(psi2);
                    if (proc2 == null) return Array.Empty<string>();
                    var out2 = proc2.StandardOutput.ReadToEndAsync();
                    if (!proc2.WaitForExit(2000))
                    {
                        try { proc2.Kill(true); } catch { }
                    }
                    output = out2.IsCompleted ? out2.Result : out2.GetAwaiter().GetResult();

                    // try to get default via lpoptions -d
                    string defaultPrinter = null;
                    try
                    {
                        var psi3 = new ProcessStartInfo
                        {
                            FileName = "lpoptions",
                            Arguments = "-d",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var proc3 = Process.Start(psi3);
                        if (proc3 != null)
                        {
                            var out3 = proc3.StandardOutput.ReadToEndAsync();
                            if (!proc3.WaitForExit(1000))
                            {
                                try { proc3.Kill(true); } catch { }
                            }
                            var txt = out3.IsCompleted ? out3.Result : out3.GetAwaiter().GetResult();
                            if (!string.IsNullOrWhiteSpace(txt))
                            {
                                // lpoptions -d PRINTER
                                defaultPrinter = txt.Trim();
                            }
                        }
                    }
                    catch { }

                    return ParseLpstatOutput(output, defaultPrinter);
                }

                return ParseLpstatOutput(output, null);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string[] ParseLpstatOutput(string output, string? defaultPrinter)
        {
            if (string.IsNullOrWhiteSpace(output)) return Array.Empty<string>();
            var names = new LinkedHashSet<string>();
            using var sr = new StringReader(output);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var trimmed = line.Trim();
                // Detect default line: "system default destination: NAME"
                if (trimmed.StartsWith("system default destination:", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = trimmed.IndexOf(':');
                    if (idx >= 0 && idx + 1 < trimmed.Length)
                        defaultPrinter = trimmed[(idx + 1)..].Trim();
                    continue;
                }
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && string.Equals(parts[0], "printer", StringComparison.OrdinalIgnoreCase))
                {
                    var name = parts[1].Trim();
                    if (!string.IsNullOrEmpty(name)) names.Add(name);
                }
            }

            var ordered = new List<string>();
            if (!string.IsNullOrEmpty(defaultPrinter) && names.Contains(defaultPrinter)) ordered.Add(defaultPrinter!);
            foreach (var n in names.OrderBy(n => n))
            {
                if (string.Equals(n, defaultPrinter, StringComparison.OrdinalIgnoreCase)) continue;
                ordered.Add(n);
            }
            return ordered.ToArray();
        }

        private static string[] GetCupsPrintersViaHttp()
        {
            // Query the local CUPS web interface and parse printer links.
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var html = client.GetStringAsync("http://localhost:631/printers/").GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(html)) return Array.Empty<string>();

                var names = new LinkedHashSet<string>();
                string? defaultPrinter = null;

                // Find default printer from common marker
                // e.g. <dt>System default destination:</dt><dd>PRINTER</dd>
                var mDefault = Regex.Match(html, "system default destination:\\s*</?[^>]*>([A-Za-z0-9._-]+)", RegexOptions.IgnoreCase);
                if (mDefault.Success && mDefault.Groups.Count > 1)
                {
                    defaultPrinter = mDefault.Groups[1].Value.Trim();
                }

                // Find links like /printers/NAME
                foreach (Match m in Regex.Matches(html, @"/printers/([^\""'\/\s]+)", RegexOptions.IgnoreCase))
                {
                    var raw = m.Groups[1].Value;
                    var parts = raw.Split(new[] { '/', '?', '"', '\'' }, StringSplitOptions.RemoveEmptyEntries);
                    var name = parts.Length > 0 ? parts[0] : raw;
                    if (!string.IsNullOrWhiteSpace(name)) names.Add(WebUtility.HtmlDecode(name));
                }

                var ordered = new List<string>();
                if (!string.IsNullOrEmpty(defaultPrinter) && names.Contains(defaultPrinter)) ordered.Add(defaultPrinter!);
                foreach (var n in names.OrderBy(n => n))
                {
                    if (string.Equals(n, defaultPrinter, StringComparison.OrdinalIgnoreCase)) continue;
                    ordered.Add(n);
                }
                return ordered.ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        // Small insertion-ordered set to preserve unique names while keeping deterministic iteration
        private class LinkedHashSet<T> : IEnumerable<T>
        {
            private readonly HashSet<T> _set = new();
            private readonly List<T> _list = new();
            public void Add(T item)
            {
                if (_set.Add(item)) _list.Add(item);
            }
            public bool Contains(T item) => _set.Contains(item);
            public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private static bool IsPrinterNameSafe(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName)) return false;
            // Evitar caracteres de control
            foreach (var ch in printerName)
            {
                if (char.IsControl(ch)) return false;
            }
            // Longitud razonable
            return printerName.Length <= 256;
        }

        private static bool IsPdf(Stream stream)
        {
            // Espera cabecera "%PDF-"
            const int headerLength = 5;
            if (!stream.CanSeek) return false;

            var current = stream.Position;
            try
            {
                stream.Position = 0;
                Span<byte> header = stackalloc byte[headerLength];
                var read = stream.Read(header);
                return read == headerLength &&
                       header[0] == (byte)'%' &&
                       header[1] == (byte)'P' &&
                       header[2] == (byte)'D' &&
                       header[3] == (byte)'F' &&
                       header[4] == (byte)'-';
            }
            finally
            {
                stream.Position = current;
            }
        }

        private static bool IsPdfContentType(string? contentType)
        {
            // Acepta PDF estricto y octet-stream (clientes que no setean bien el tipo)
            return string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLabelContentType(string? contentType)
        {
            // ZPL/EPL suele venir como text/plain u octet-stream
            return string.Equals(contentType, "text/plain", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsJsonContentType(string? contentType)
        {
            // Acepta application/json y octet-stream (por si el cliente no setea bien el tipo)
            return string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        
    }
}
