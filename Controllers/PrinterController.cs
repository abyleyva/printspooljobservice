using ICSharpCode.Decompiler.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using POSPrinter;
using PrintSpoolJobService.Models;
using Spire.Pdf;
using Spire.Pdf.Print;
using System.Collections;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Resources;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static ICSharpCode.Decompiler.IL.Transforms.Stepper;
using static PrintSpoolJobService.Models.Ticket;

namespace PrintSpoolJobService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]    
    public class PrinterController : ControllerBase
    {
        private readonly ILogger<PrinterController>? _logger;
        private readonly IWebHostEnvironment _env;
        private static readonly object _resxLock = new();

        

        public PrinterController(ILogger<PrinterController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        [HttpGet("logo-keys")]
        public IActionResult GetLogoKeys()
        {
            var resourcesDir = Path.Combine(_env.ContentRootPath ?? Directory.GetCurrentDirectory(), "Resources");
            var resxPath = Path.Combine(resourcesDir, "Logos.resx");
            if (!System.IO.File.Exists(resxPath)) return Ok(Array.Empty<object>());

            try
            {
                Dictionary<string, object> existing = new(StringComparer.OrdinalIgnoreCase);
                lock (_resxLock)
                {
                    var doc = System.Xml.Linq.XDocument.Load(resxPath);
                    foreach (var data in doc.Root?.Elements("data") ?? Enumerable.Empty<System.Xml.Linq.XElement>())
                    {
                        var name = data.Attribute("name")?.Value;
                        if (string.IsNullOrEmpty(name)) continue;
                        var valueElem = data.Element("value");
                        if (valueElem == null) continue;
                        var text = valueElem.Value ?? string.Empty;
                        try
                        {
                            var decoded = Convert.FromBase64String(text);
                            existing[name] = decoded;
                        }
                        catch
                        {
                            existing[name] = text;
                        }
                    }
                }

                var keys = existing
                    .Where(kv => !(kv.Key.EndsWith(".contentType", StringComparison.OrdinalIgnoreCase) || kv.Key.EndsWith(".filename", StringComparison.OrdinalIgnoreCase)))
                    .Where(kv => kv.Value is byte[])
                    .Select(kv => new
                    {
                        key = kv.Key,
                        filename = existing.TryGetValue(kv.Key + ".filename", out var f) ? f as string : null,
                        contentType = existing.TryGetValue(kv.Key + ".contentType", out var ct) ? ct as string : null
                    })
                    .OrderBy(x => x.key)
                    .ToArray();

                return Ok(keys);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error listing logo keys");
                return StatusCode(500, "Internal server error - list logo keys");
            }
        }

        [HttpGet("logo")]
        public IActionResult GetLogo([FromQuery] string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest("Resource key is required");

            var keyPattern = new Regex("^[A-Za-z0-9._-]{1,200}$", RegexOptions.Compiled);
            if (!keyPattern.IsMatch(key))
                return BadRequest("Invalid resource key");

            var resourcesDir = Path.Combine(_env.ContentRootPath ?? Directory.GetCurrentDirectory(), "Resources");
            var resxPath = Path.Combine(resourcesDir, "Logos.resx");
            if (!System.IO.File.Exists(resxPath)) return NotFound("No logos resource file found");

            try
            {
                var doc = System.Xml.Linq.XDocument.Load(resxPath);
                var data = doc.Root?.Elements("data").FirstOrDefault(e => string.Equals(e.Attribute("name")?.Value, key, StringComparison.OrdinalIgnoreCase));
                if (data == null) return NotFound("Logo not found");

                var valueElem = data.Element("value");
                if (valueElem == null) return NotFound("Logo value missing");

                byte[] bytes;
                var text = valueElem.Value ?? string.Empty;
                try
                {
                    bytes = Convert.FromBase64String(text);
                }
                catch
                {
                    return NotFound("Logo data is not binary");
                }

                // try to find stored contentType
                var ctElem = doc.Root?.Elements("data").FirstOrDefault(e => string.Equals(e.Attribute("name")?.Value, key + ".contentType", StringComparison.OrdinalIgnoreCase));
                string contentType = ctElem?.Element("value")?.Value ?? DetectContentType(bytes);

                return File(bytes, contentType);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error serving logo {Key}", key);
                return StatusCode(500, "Internal server error - serve logo");
            }
        }
        [HttpPut("save_logo")]
        [RequestSizeLimit(5_000_000)] // 5 MB
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SaveLogo(IFormFile? logo, [FromForm] string? resourceKey, CancellationToken ct)
        {
            if (logo is null || logo.Length == 0)
                return BadRequest("Logo file cannot be null or empty");

            var allowed = new[] { "image/png", "image/jpeg", "image/svg+xml", "image/webp" };
            if (!allowed.Contains(logo.ContentType, StringComparer.OrdinalIgnoreCase))
                return BadRequest("Content-Type must be one of: image/png, image/jpeg, image/svg+xml, image/webp");

            const long maxBytes = 5_000_000; // 5 MB
            if (logo.Length > maxBytes)
                return BadRequest($"Logo file too large. Max allowed is {maxBytes} bytes");

            try
            {
                ct.ThrowIfCancellationRequested();

                var original = Path.GetFileName(logo.FileName ?? string.Empty);
                if (string.IsNullOrWhiteSpace(original))
                    return BadRequest("Invalid file name");

                // Basic filename safety
                if (original.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || original.Contains(".."))
                    return BadRequest("Invalid file name");

                var ext = Path.GetExtension(original);
                if (string.IsNullOrEmpty(ext))
                {
                    ext = logo.ContentType switch
                    {
                        "image/png" => ".png",
                        "image/jpeg" => ".jpg",
                        "image/svg+xml" => ".svg",
                        "image/webp" => ".webp",
                        _ => ".img"
                    };
                }

                // resource key to store inside the .resx
                var key = string.IsNullOrWhiteSpace(resourceKey) ? "logo" : resourceKey.Trim();
                // Validate key with a safe pattern: allow letters, digits, dot, underscore and hyphen
                var keyPattern = new Regex("^[A-Za-z0-9._-]{1,200}$", RegexOptions.Compiled);
                if (!keyPattern.IsMatch(key))
                    return BadRequest("Invalid resource key. Allowed: A-Z a-z 0-9 . _ - (max 200 chars)");

                // Read uploaded content into memory
                await using var input = logo.OpenReadStream();
                using var ms = new MemoryStream(capacity: (int)logo.Length);
                await input.CopyToAsync(ms, ct);
                var bytes = ms.ToArray();

                // Prepare Resources directory and .resx file path
                var resourcesDir = Path.Combine(_env.ContentRootPath ?? Directory.GetCurrentDirectory(), "Resources");
                Directory.CreateDirectory(resourcesDir);
                var resxPath = Path.Combine(resourcesDir, "Logos.resx");

                // Load existing resources (if any) using simple XML read to avoid dependency on ResX types
                var existing = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                if (System.IO.File.Exists(resxPath))
                {
                    try
                    {
                        var doc = System.Xml.Linq.XDocument.Load(resxPath);
                        foreach (var data in doc.Root?.Elements("data") ?? Enumerable.Empty<System.Xml.Linq.XElement>())
                        {
                            var name = data.Attribute("name")?.Value;
                            if (string.IsNullOrEmpty(name)) continue;
                            var valueElem = data.Element("value");
                            if (valueElem == null) continue;
                            var text = valueElem.Value ?? string.Empty;
                            // try decode base64 for byte[] resources
                            try
                            {
                                var decoded = Convert.FromBase64String(text);
                                existing[name] = decoded;
                            }
                            catch
                            {
                                existing[name] = text;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to read existing .resx (XML), will overwrite");
                        existing.Clear();
                    }
                }

                // Replace or add the resource key with the byte[] content
                existing[key] = bytes;
                // Also store content type and original filename as separate resources
                existing[$"{key}.contentType"] = logo.ContentType ?? "application/octet-stream";
                existing[$"{key}.filename"] = original;

                // Write resources back to the .resx file atomically and thread-safe within process
                var tempResx = Path.Combine(resourcesDir, $"{Guid.NewGuid():N}.resx.tmp");
                lock (_resxLock)
                {
                    try
                    {
                        var doc = new System.Xml.Linq.XDocument(
                            new System.Xml.Linq.XDeclaration("1.0", "utf-8", "yes"),
                            new System.Xml.Linq.XElement("root",
                                new System.Xml.Linq.XElement("resheader", new System.Xml.Linq.XAttribute("name", "resmimetype"),
                                    new System.Xml.Linq.XElement("value", "text/microsoft-resx")),
                                new System.Xml.Linq.XElement("resheader", new System.Xml.Linq.XAttribute("name", "version"),
                                    new System.Xml.Linq.XElement("value", "2.0")),
                                new System.Xml.Linq.XElement("resheader", new System.Xml.Linq.XAttribute("name", "reader"),
                                    new System.Xml.Linq.XElement("value", "System.Resources.ResXResourceReader")),
                                new System.Xml.Linq.XElement("resheader", new System.Xml.Linq.XAttribute("name", "writer"),
                                    new System.Xml.Linq.XElement("value", "System.Resources.ResXResourceWriter"))
                            )
                        );

                        foreach (var kv in existing.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                        {
                            var dataElem = new System.Xml.Linq.XElement("data", new System.Xml.Linq.XAttribute("name", kv.Key));
                            if (kv.Value is byte[] b)
                            {
                                dataElem.Add(new System.Xml.Linq.XElement("value", Convert.ToBase64String(b)));
                                dataElem.Add(new System.Xml.Linq.XAttribute("type", "System.Byte[]"));
                            }
                            else
                            {
                                dataElem.Add(new System.Xml.Linq.XElement("value", kv.Value?.ToString() ?? string.Empty));
                            }
                            doc.Root!.Add(dataElem);
                        }

                        doc.Save(tempResx);

                        if (System.IO.File.Exists(resxPath))
                        {
                            var backup = resxPath + ".bak";
                            System.IO.File.Replace(tempResx, resxPath, backup);
                            try { if (System.IO.File.Exists(backup)) System.IO.File.Delete(backup); } catch { }
                        }
                        else
                        {
                            System.IO.File.Move(tempResx, resxPath);
                        }
                    }
                    finally
                    {
                        try { if (System.IO.File.Exists(tempResx)) System.IO.File.Delete(tempResx); } catch { }
                    }
                }

                _logger?.LogInformation("Saved logo into .resx key={Key} Size={Size}", key, bytes.Length);

                // Do not reveal server file system paths to clients
                return Ok(new { key });
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Save logo canceled by client");
                return StatusCode(499, "Client Closed Request");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving logo");
                return StatusCode(500, "Internal server error - save logo");
            }
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
#pragma warning disable CA1416 // Validate platform compatibility
                    printers = PrinterSettings
                    .InstalledPrinters
                    .Cast<string>().OrderBy(p => p).ToArray();
#pragma warning restore CA1416 // Validate platform compatibility

                    if (printers.Length == 0)
                    {
                        _logger?.LogWarning("No printers found");
                    }
                }

                // 200 Printer List (maybe empty) is more predictable for the client
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
                // 1) Local IP of the HTTP socket (useful behind reverse proxy or multiple NICs)
                var httpLocal = HttpContext?.Connection?.LocalIpAddress;

                // 2) Collect addresses of operational and non-virtual/tunnel NICs
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

                // Valid IPv4 addresses
                var v4 = unicast
                    .Select(x => x.Address)
                    .Where(ip =>
                        ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ip) &&
                        !IsApipa(ip) &&
                        !ip.Equals(IPAddress.Any))
                    .Distinct()
                    .ToList();

                // Valid IPv6 addresses (not link-local, Teredo, multicast, unspecified)
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

                // Simple heuristic for a "primary" address: prioritize addresses with a gateway
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
                        if (v4.Count == 0) return NotFound("No valid IPv4 addresses found");
                        response = new
                        {
                            hostname = resultAll.hostname,
                            httpLocal = resultAll.httpLocal,
                            ipv4 = resultAll.ipv4,
                            primaryV4 = resultAll.primaryV4
                        };
                        break;

                    case "ipv6":
                        if (v6.Count == 0) return NotFound("No valid IPv6 addresses found");
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
                            return NotFound("No primary IP address found");
                        response = new
                        {
                            hostname = resultAll.hostname,
                            httpLocal = resultAll.httpLocal,
                            primaryV4 = resultAll.primaryV4,
                            primaryV6 = resultAll.primaryV6
                        };
                        break;

                    case "httplocal":
                        if (httpLocal is null) return NotFound("No local IP of the HTTP socket found");
                        response = new
                        {
                            hostname = resultAll.hostname,
                            httpLocal = resultAll.httpLocal
                        };
                        break;

                    case "all":
                    default:
                        if (v4.Count == 0 && v6.Count == 0 && httpLocal is null)
                            return NotFound("No valid local IP addresses found");
                        response = resultAll;
                        break;
                }

                _logger?.LogInformation("Local IPs detected. IPv4={CountV4}, IPv6={CountV6}, select={Select}", v4.Count, v6.Count, sel);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error obtaining local IP addresses");
                return StatusCode(500, "Internal server error - local IPs");
            }
        }

        // Print PDFs Files
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

        // Print EZPL/ZPL as RAW like Printers Zebra
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
        public async Task<IActionResult> PrintTicket([FromBody] JsonElement request)
        {
            try
            {
                if (request.ValueKind == JsonValueKind.Undefined || request.ValueKind == JsonValueKind.Null)
                    return BadRequest("Request body cannot be null or empty");

                // Deserialize the dynamic JSON into Ticket object
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var ticket = JsonSerializer.Deserialize<Ticket>(request.GetRawText(), options);
                if (ticket?.newTicket == null)
                    return BadRequest("Invalid ticket structure. Expected 'newTicket' property");

                var config = ticket.newTicket;

                // Validate required fields
                if (string.IsNullOrWhiteSpace(config.PrinterName))
                    return BadRequest("PrinterName is required");

                if (!IsPrinterNameSafe(config.PrinterName))
                    return BadRequest("Printer name contains invalid characters");

                if (!PrinterExists(config.PrinterName))
                    return NotFound($"Printer '{config.PrinterName}' not found");

                if (config.Operations == null || config.Operations.Count == 0)
                    return BadRequest("Operations list cannot be empty");

                // Log incoming ticket
                _logger?.LogInformation(
                    "Received ticket for printer {Printer} with {Count} operations",
                    config.PrinterName, config.Operations.Count);

                // Process the ticket
                var result = await ProcessTicketAsync(config);

                return Ok(new
                {
                    success = true,
                    message = "Ticket processed successfully",
                    printerName = config.PrinterName,
                    operationsProcessed = config.Operations.Count
                });
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "Invalid JSON format in ticket request");
                return BadRequest($"Invalid JSON format: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Print ticket processing canceled by client");
                return StatusCode(499, "Client Closed Request");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing ticket");
                return StatusCode(500, "Internal server error - processing ticket");
            }
        }

        private async Task<bool> ProcessTicketAsync(Ticket.TicketConfig config)
        {
            var printerName = config.PrinterName.Trim();
            Encoding encoding = Encoding.GetEncoding(config.Encoding ?? "UTF-8"); // Default encoding, can be adjusted per operation if needed
            var bytes = new List<byte>();
            var tkt= new POSPrinter.POSTicket();

            foreach (var operation in config.Operations)
            {
                if (string.IsNullOrWhiteSpace(operation.Action))
                {
                    _logger?.LogWarning("Skipping operation with null or empty action");
                    continue;
                }

                var action = operation.Action.Trim();
                var args = operation.Args ?? new List<object>();

                _logger?.LogDebug("Processing operation: {Action} with {ArgCount} arguments", action, args.Count);

                switch (action)
                {
                    case "Header":
                        tkt.Bold(true);
                        foreach (var arg in args)
                        {
                            // Puedes procesar cada argumento según el tipo
                            tkt.AddHeader(arg?.ToString() ?? string.Empty);
                        }
                        tkt.Bold(false);
                        break;
                    case "HeaderItem":
                        // Espera que cada arg sea un array/lista de 3 elementos: texto, ancho, alineación
                        var columns = args
                            .Select(arg =>
                            {
                                if (arg is JsonElement elem && elem.ValueKind == JsonValueKind.Array && elem.GetArrayLength() == 3)
                                {
                                    var text = elem[0].GetString() ?? "";
                                    var width = elem[1].GetInt32();
                                    var align = (POSTicket.TextAlign)elem[2].GetInt32();
                                    tkt.EnableWrap(true,POSTicket.WrapMode.Word);
                                    return (text, width, align);
                                }
                                // fallback: columna fija si el formato no es correcto
                                return ("", 8, POSTicket.TextAlign.Left);
                            })
                            .ToArray();
                        //tkt.AddColumns(columns);
                        tkt.AddColumnsWrapped(columns);
                        break;
                    case "RowItem":
                        tkt.EnableWrap(true, POSTicket.WrapMode.Word);
                        foreach (var arg in args)
                        {
                            //elem.GetArrayLength() Fijo 4 Elementos: code, name, qty, price
                            if (arg is JsonElement elem && elem.ValueKind == JsonValueKind.Array && elem.GetArrayLength() == 4) 
                            {
                                var code = elem[0].GetString() ?? string.Empty;
                                var name = elem[1].GetString() ?? string.Empty;
                                var quantity = elem[2].ValueKind == JsonValueKind.Number ? elem[2].GetDecimal() : Convert.ToDecimal(elem[2].ToString());
                                var price = elem[3].ValueKind == JsonValueKind.Number ? elem[3].GetDecimal() : Convert.ToDecimal(elem[3].ToString());
                                tkt.AddItem(code, name, quantity, price);
                            }
                        }
                        break;
                    case "CustomItem":
                        // Espera que cada arg sea un array de arrays, donde cada array interno tiene 3 elementos: texto, ancho, alineación
                        foreach (var arg in args)
                        {
                            if (arg is JsonElement elem && elem.ValueKind == JsonValueKind.Array)
                            {
                                var CRows = new List<(string text, int width, POSTicket.TextAlign align)>();
                                
                                // Iterar sobre cada elemento del array (que debería ser un array de 3 elementos)
                                for (int i = 0; i < elem.GetArrayLength(); i++)
                                {
                                    var rowItem = elem[i];
                                    if (rowItem.ValueKind == JsonValueKind.Array && rowItem.GetArrayLength() == 3)
                                    {
                                        var text = rowItem[0].GetString() ?? "";
                                        var width = rowItem[1].GetInt32();
                                        var align = (POSTicket.TextAlign)rowItem[2].GetInt32();
                                        tkt.EnableWrap(true, POSTicket.WrapMode.Word);
                                        CRows.Add((text, width, align));
                                    }
                                }
                                
                                // Añadir la fila al ticket si tiene columnas
                                if (CRows.Count > 0)
                                {
                                    tkt.AddCustomItem(CRows.ToArray());
                                }
                            }
                        }
                        break;
                    case "Body":
                        foreach (var arg in args)
                        {
                            tkt.AddBody(arg?.ToString() ?? string.Empty);
                        }
                        break;
                    case "Footer":
                        foreach (var arg in args)
                        {
                            tkt.AddFooter(arg?.ToString() ?? string.Empty);
                        }
                        break;

                    case "Text":
                        foreach (var arg in args)
                        {
                            tkt.Text(arg?.ToString() ?? string.Empty);
                        }
                        break;

                    case "AlignLeft":
                        tkt.AlignLeft();
                        break;

                    case "AlignRight":
                        tkt.AlignRight();
                        break;

                    case "AlignCenter":
                        tkt.AlignCenter();
                        break;

                    case "Feed":
                        tkt.Feed(args.Count > 0 && int.TryParse(args[0]?.ToString(), out var lines) ? lines : 1);
                        break;

                    case "ContinueLine":
                        tkt.ContinueLine(char.Parse(args.Count > 0 ? (args[0]?.ToString()) ?? "-" : "-"));                        
                        break;

                    case "PrintQRCode":
                        if (args.Count >= 1)
                        {
                            var content = args[0]?.ToString() ?? string.Empty;
                            var size = args.Count > 1 && int.TryParse(args[1]?.ToString(), out var s) ? s : 5;
                            var qrBytes = GenerateQRCodeCommand(content, size);
                            bytes.AddRange(qrBytes);
                        }
                        break;

                    case "LoadImageFromURLAndPrint":
                        if (args.Count >= 1)
                        {
                            var url = args[0]?.ToString();
                            if (!string.IsNullOrWhiteSpace(url))
                            {
                                try
                                {
                                    var imageBytes = await DownloadImageAsync(url);
                                    if (imageBytes != null && imageBytes.Length > 0)
                                    {
                                        var printCmd = ConvertImageToPrinterFormat(imageBytes);
                                        bytes.AddRange(printCmd);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogWarning(ex, "Failed to download or process image from {Url}", url);
                                }
                            }
                        }
                        break;

                    case "PaperCut":
                        if (args.Count > 0 && Boolean.TryParse(args[0]?.ToString(), out bool full))
                        {
                            tkt.Cut(full);
                        }
                        break;

                    case "Beep":
                        if (args.Count > 0 && int.TryParse(args[0]?.ToString(), out var times))
                        {
                            for (int i = 0; i < times; i++)
                                bytes.AddRange(new byte[] { 0x1B, 0x42, 0x03, 0x03 }); // ESC B
                        }
                        break;

                    case "OpenCashDrawer":
                        bytes.AddRange(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }); // ESC p (Kick drawer)
                        break;

                    case "Reset":
                        tkt.Initialize();
                        //bytes.AddRange(new byte[] { 0x1B, 0x40 }); // ESC @
                        break;

                    default:
                        _logger?.LogWarning("Unknown operation: {Action}", action);
                        break;
                }
            }

            // Send the complete job to the printer
            if (bytes.Count >= 0)
            {
                try
                {
                    tkt.Print(printerName);
                    //RawPrinterHelper.SendRawJob(printerName, bytes.ToArray(), "Ticket RAW Job");
                    _logger?.LogInformation(
                        "Ticket job sent to printer {Printer}. Size={Size} bytes",
                        printerName, bytes.Count);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send ticket to printer {Printer}", printerName);
                    throw;
                }
            }

            return false;
        }

        private byte[] GenerateQRCodeCommand(string content, int size)
        {
            // ESC i command format for QR code
            // This is a basic implementation - adjust based on your thermal printer model
            var bytes = new List<byte> { 0x1B, 0x69 }; // ESC i
            bytes.AddRange(Encoding.UTF8.GetBytes(content));
            bytes.Add(0x00);
            return bytes.ToArray();
        }

        private async Task<byte[]?> DownloadImageAsync(string url)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error downloading image from {Url}", url);
                return null;
            }
        }

        private byte[] ConvertImageToPrinterFormat(byte[] imageBytes)
        {
            // This is a placeholder - you'll need to implement proper image-to-printer-format conversion
            // For most thermal printers, this involves:
            // 1. Loading the image
            // 2. Converting to monochrome (1-bit)
            // 3. Resizing to printer width (usually 384 or 576 pixels)
            // 4. Converting to ESC/POS format
            // For now, just return a basic structure
            var bytes = new List<byte> { 0x1B, 0x2A };
            bytes.AddRange(imageBytes);
            return bytes.ToArray();
        }
        
        // ----------------- Private Helpers -----------------

        private async Task<IActionResult> PrintPdfInternalAsync(IFormFile pdfFile, string printerName, CancellationToken ct)
        {
            try
            {
                await using var input = pdfFile.OpenReadStream(); //Retrieve stream of the PDF file
                using var buffered = new MemoryStream(); // ensures Seek and header validation
                await input.CopyToAsync(buffered, ct);

                if (!IsPdf(buffered))
                {
                    return BadRequest("The uploaded file is not a valid PDF");
                }

                buffered.Position = 0;

                using var pdf = new PdfDocument();
                pdf.LoadFromStream(buffered);

                // Verification: document without pages
                if (pdf.Pages.Count == 0)
                {
                    _logger?.LogWarning("Rejected PDF with zero pages for printer {Printer}", printerName);
                    return BadRequest("The PDF document has no pages");
                }

                // Verification: empty pages (text-based heuristic)
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
                return StatusCode(500, "Internal server error - Error printing PDF Document");
            }
        }
        
        private static bool PrinterExists(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName)) return false;
            var target = printerName.Trim();
            if (OperatingSystem.IsWindows())
            {
#pragma warning disable CA1416 // Validate platform compatibility
                return PrinterSettings
                    .InstalledPrinters
                    .Cast<string>().Any(p => string.Equals(p, target, StringComparison.OrdinalIgnoreCase));
#pragma warning restore CA1416 // Validate platform compatibility
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
                    string? defaultPrinter = null;
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
            // Avoid control characters
            foreach (var ch in printerName)
            {
                if (char.IsControl(ch)) return false;
            }
            // Reasonable length
            return printerName.Length <= 256;
        }

        private static bool IsPdf(Stream stream)
        {
            // Expect header "%PDF-"
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
            // Accepts strict PDF and octet-stream (clients that do not set the type correctly)
            return string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLabelContentType(string? contentType)
        {
            // ZPL/EPL usually comes as text/plain or octet-stream
            return string.Equals(contentType, "text/plain", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsJsonContentType(string? contentType)
        {
            // Accepts application/json and octet-stream (in case the client does not set the type correctly)
            return string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        private static string DetectContentType(byte[] b)
        {
            if (b == null || b.Length == 0) return "application/octet-stream";
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
            // JPEG: FF D8
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xD8) return "image/jpeg";
            // WEBP: starts with "RIFF"...."WEBP"
            if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
            // SVG: starts with '<' and contains "svg" marker
            try
            {
                var s = Encoding.UTF8.GetString(b, 0, Math.Min(b.Length, 512)).ToLowerInvariant();
                if (s.TrimStart().StartsWith("<svg") || s.Contains("<svg")) return "image/svg+xml";
            }
            catch { }
            return "application/octet-stream";
        }
    }
}
