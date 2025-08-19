using Microsoft.AspNetCore.Mvc;
using Spire.Pdf;
using Spire.Pdf.Print;
using System.Drawing.Printing;
using System.Net;


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
                // Fetching printer list
                var printers = PrinterSettings.InstalledPrinters.Cast<string>()
                    .Select(name =>  name) // Assuming all printers are online for simplicity
                    .ToArray();
                if (printers.Length == 0)
                {
                    _logger?.LogWarning("No printers found");
                    return NotFound("No printers found");
                }

                return Ok(printers);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching printer list");
                return StatusCode(500, "Internal server error - printers");
            }
        }

        [HttpPost("print-ticket")]
        public IActionResult PrintPosTicket(IFormFile fileDocument, [FromForm] string printerName)
        {
            try
            {
                if (fileDocument == null || fileDocument.Length == 0)
                {
                    return BadRequest("File document cannot be null or empty");
                }

                if (string.IsNullOrWhiteSpace(printerName))
                {
                    return BadRequest("Printer name cannot be null or empty");
                }
                
                // Check if the specified printer is available
                if (!PrinterSettings.InstalledPrinters.Cast<string>().Contains(printerName))
                {
                    return NotFound($"Printer '{printerName}' not found");
                }

                // save the PDF file to a temporary location and temporal file name
                var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    fileDocument.CopyTo(stream);
                }

                //load PDF file
                PdfDocument pdfDocument = new();

                // Load the PDF document from the temporary file
                pdfDocument.LoadFromFile(tempFilePath);

                // Here you would typically set the printer settings
                PdfPrintSettings printerSettings = new PdfPrintSettings();

                printerSettings.PrinterName = printerName;
                printerSettings.SelectPageRange(1, pdfDocument.Pages.Count); // Print all pages
                printerSettings.SelectSinglePageLayout(PdfSinglePageScalingMode.FitSize);


                // Print the document
                pdfDocument.Print(printerSettings);

                // Clean up the temporary file
                pdfDocument.Close();
                System.IO.File.Delete(tempFilePath);

                return Ok("POS ticket printed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error printing POS ticket");
                return StatusCode(500, "Internal server error - Error print pos ticket");
            }
        }
        //Print PDFs Files
        [HttpPost("print-pdf")]
        public IActionResult PrintPDF(IFormFile documentPDF, [FromForm] string printerName)
        {
            try
            {
                if (documentPDF == null || documentPDF.Length == 0)
                {
                    return BadRequest("PDF document cannot be null or empty");
                }

                if (string.IsNullOrWhiteSpace(printerName))
                {
                    return BadRequest("Printer name cannot be null or empty");
                }

                // Check if the specified printer is available
                if (!PrinterSettings.InstalledPrinters.Cast<string>().Contains(printerName))
                {
                    return NotFound($"Printer '{printerName}' not found");
                }

                // save the PDF file to a temporary location and temporal file name
                var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    documentPDF.CopyTo(stream);
                }

                //load PDF file
                PdfDocument pdfDocument = new();

                // Load the PDF document from the temporary file
                pdfDocument.LoadFromFile(tempFilePath);

                // Here you would typically set the printer settings
                PdfPrintSettings printerSettings = new PdfPrintSettings();

                printerSettings.PrinterName = printerName;
                printerSettings.SelectPageRange(1, pdfDocument.Pages.Count); // Print all pages
                printerSettings.SelectSinglePageLayout(PdfSinglePageScalingMode.FitSize);


                // Print the document
                pdfDocument.Print(printerSettings);

                // Clean up the temporary file
                pdfDocument.Close();
                System.IO.File.Delete(tempFilePath);

                return Ok("PDF document printed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error printing PDF Document");
                return StatusCode(500, "Internal server error - Error print PDF Document");
            }
        }
        //Print EZPL Files To Zebra Printers
        [HttpPost("print-label")]
        public IActionResult PrintLabel(IFormFile documentEZPL, [FromForm] string printerName)
        {
            try
            {
                if (documentEZPL == null || documentEZPL.Length == 0)
                {
                    return BadRequest("EZPL Document cannot be null or empty");
                }

                if (string.IsNullOrWhiteSpace(printerName))
                {
                    return BadRequest("Printer name cannot be null or empty");
                }

                // Check if the specified printer is available
                if (!PrinterSettings.InstalledPrinters.Cast<string>().Contains(printerName))
                {
                    return NotFound($"Printer '{printerName}' not found");
                }

                // save the PDF file to a temporary location and temporal file name
                var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    documentEZPL.CopyTo(stream);
                }

                //load PDF file
                PdfDocument pdfDocument = new();

                // Load the PDF document from the temporary file
                pdfDocument.LoadFromFile(tempFilePath);

                // Here you would typically set the printer settings
                PdfPrintSettings printerSettings = new PdfPrintSettings();

                printerSettings.PrinterName = printerName;
                printerSettings.SelectPageRange(1, pdfDocument.Pages.Count); // Print all pages
                printerSettings.SelectSinglePageLayout(PdfSinglePageScalingMode.FitSize);


                // Print the document
                pdfDocument.Print(printerSettings);

                // Clean up the temporary file
                pdfDocument.Close();
                System.IO.File.Delete(tempFilePath);

                return Ok("EZPL document printed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error printing EZPL Document");
                return StatusCode(500, "Internal server error - Error print EZPL Document");
            }
        }

        [HttpGet("get-local-ipaddress")]
        public IActionResult GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ipAddressV4 = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                var ipAddressV6 = host.AddressList.FirstOrDefault(ipv6 => ipv6.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);

                if (ipAddressV4 == null)
                {
                    return NotFound("No local IP address found");
                }
                return Ok(new
                {
                    hostname = host.HostName,
                    localipV4 = ipAddressV4.ToString(),
                    localipV6 = ipAddressV6?.ToString() ?? "No IPv6 address found"
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching local IP address");
                return StatusCode(500, "Internal server error - Error fetching local IP address");
            }
        }
        public static void PrintTicketPOS()
        {
            string printerName = "YourPrinterName"; // Replace with your printer name
            string textToPrint = "Hello, this is a test print from PrintSpoolJobService!";
            try
            {
                // Create a new PrintDocument
                PrintDocument printDocument = new PrintDocument();
                printDocument.PrinterSettings.PrinterName = printerName;
                // Check if the printer is valid
                if (!printDocument.PrinterSettings.IsValid)
                {
                    throw new Exception($"Printer '{printerName}' is not valid.");
                }
                // Set the PrintPage event handler
                printDocument.PrintPage += (sender, e) =>
                {
                    //e.Graphics.DrawString(textToPrint, new Font("Arial", 12), Brushes.Black, 10, 10);
                };
                // Print the document
                printDocument.Print();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error printing ticket: {ex.Message}");
            }

        }
    }
}
