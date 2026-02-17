using Microsoft.AspNetCore.Mvc;
using PrintSpoolJobService.Models;
using Spire.Pdf;
using Spire.Pdf.Print;
using System.Drawing.Printing;
using System.Globalization;
using System.Net;
using System.Text;

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
                var printers = PrinterSettings
                    .InstalledPrinters
                    .Cast<string>()
                    .OrderBy(p => p)
                    .ToArray();

                if (printers.Length == 0)
                {
                    _logger?.LogWarning("No printers found");
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
            return PrinterSettings
                .InstalledPrinters
                .Cast<string>()
                .Any(p => string.Equals(p, target, StringComparison.OrdinalIgnoreCase));
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
