
![Logo](https://avatars.githubusercontent.com/u/43762225?s=96&v=4)

---

# 🖨️ Print Spool Job Service - Public API

**A comprehensive RESTful web service for managing printing operations across networked printers.**

A robust, production-grade printing service that provides a unified API for managing diverse printing tasks including PDF documents, label printing (ZPL/EZPL), and thermal receipts. Built with .NET 10, this service runs as a Windows Service with full CORS support and Swagger documentation.

## 📋 Quick Links

- **Base URL**: `http://localhost:5075/api/printer` or `http://{your-ip}:5075/api/printer`
- **Documentation**: Swagger UI available at `http://localhost:5075/swagger` (Development mode)
- **Repository**: [@abyleyva/printspooljobservice](https://www.github.com/abyleyva/printspooljobservice)

---

## ✨ Key Features

✅ **Multi-Format Printing Support**
- PDF document printing with validation and page detection
- ZPL/EZPL label printing for Zebra printers
- Thermal receipt/ticket printing with advanced formatting

✅ **Infrastructure & Discovery**
- Automatic printer enumeration (Windows & CUPS/Linux)
- Network IP address detection (IPv4 & IPv6)
- Multi-platform support (Windows, Linux)

✅ **Resource Management**
- Logo/image storage and retrieval via .resx resources
- Support for PNG, JPEG, SVG, and WebP formats
- Base64 encoded binary storage

✅ **Enterprise Features**
- Windows Service hosting support
- CORS enabled for cross-origin requests
- Comprehensive error handling and logging
- Request size limits and validation
- Cancellation token support for long-running operations

---

## 📡 API Reference

### 1. **Get Available Printers**

Returns a list of all printers installed on the system (Windows) or configured in CUPS (Linux).

```http
GET /api/printer/get-printers
```

**Parameters**: None

**Response** (200 OK):
```json
[
  "Brother HL-L2350DW",
  "HP LaserJet Pro M404n",
  "Zebra GX430t"
]
```

**Response** (501 Not Implemented):
```json
{
  "message": "Printer enumeration not implemented on this platform",
  "reason": "CUPS (lpstat) not available or no printers configured",
  "resolution": "Install CUPS or enable the CUPS web interface at http://localhost:631"
}
```

---

### 2. **Get Local IP Addresses**

Retrieves the local network IP addresses of the host machine, supporting both IPv4 and IPv6.

```http
GET /api/printer/get-local-ipaddress?select={filter}
```

**Query Parameters**:
| Parameter | Type | Default | Description |
|:----------|:-----|:--------|:------------|
| `select` | string | `all` | Filter: `all`, `ipv4`, `ipv6`, `primary`, `httplocal` |

**Response** (200 OK) - All addresses:
```json
{
  "hostname": "mycomputer",
  "httpLocal": "192.168.1.10",
  "ipv4": ["192.168.1.10", "192.168.1.11"],
  "ipv6": ["fe80::1", "2001:db8::1"],
  "primaryV4": "192.168.1.10",
  "primaryV6": "fe80::1"
}
```

**Response** (200 OK) - IPv4 only:
```json
{
  "hostname": "mycomputer",
  "httpLocal": "192.168.1.10",
  "ipv4": ["192.168.1.10"],
  "primaryV4": "192.168.1.10"
}
```

---

### 3. **Print PDF Document**

Prints a PDF file to the specified printer with automatic validation.

```http
POST /api/printer/print-pdf
Content-Type: multipart/form-data
```

**Form Parameters**:
| Parameter | Type | Required | Description |
|:----------|:-----|:---------|:------------|
| `documentPDF` | file | ✅ Yes | PDF file (Content-Type: `application/pdf`), max 10 MB |
| `printerName` | string | ✅ Yes | Target printer name (from `/get-printers`) |

**cURL Example**:
```bash
curl -X POST http://localhost:5075/api/printer/print-pdf \
  -F "documentPDF=@invoice.pdf" \
  -F "printerName=Brother HL-L2350DW"
```

**Response** (200 OK):
```json
{
  "message": "PDF document printed successfully"
}
```

**Error Responses**:
- `400 Bad Request`: Missing file, invalid Content-Type, or invalid printer name
- `404 Not Found`: Printer not found
- `500 Internal Server Error`: PDF processing error

---

### 4. **Print Label (ZPL/EZPL)**

Prints label files in ZPL (Zebra Programming Language) or EZPL format to thermal label printers.

```http
POST /api/printer/print-label
Content-Type: multipart/form-data
```

**Form Parameters**:
| Parameter | Type | Required | Description |
|:----------|:-----|:---------|:------------|
| `documentEZPL` | file | ✅ Yes | ZPL/EZPL file (Content-Type: `text/plain`), max 2 MB |
| `printerName` | string | ✅ Yes | Target Zebra printer name |

**cURL Example**:
```bash
curl -X POST http://localhost:5075/api/printer/print-label \
  -F "documentEZPL=@label.txt" \
  -F "printerName=Zebra GX430t"
```

**Sample ZPL Content** (label.txt):
```zpl
^XA
^FO50,50^A0N,25,25^FDShipping Label^FS
^BY2,3,50
^FO50,100^BC^FD123456789^FS
^FO50,200^A0N,20,20^FDWeight: 2.5 kg^FS
^XZ
```

**Response** (200 OK):
```json
{
  "message": "EZPL document printed successfully"
}
```

---

### 5. **Print Ticket (Thermal Receipt)**

Prints formatted thermal receipts/tickets with advanced layout control (headers, items, footers, logos, etc.).

```http
POST /api/printer/print-ticket
Content-Type: application/json
```

**Request Body**:
```json
{
  "newTicket": {
    "printerName": "Epson TM-T20II",
    "encoding": "UTF-8",
    "operations": [
      {
        "action": "Reset",
        "args": []
      },
      {
        "action": "Header",
        "args": ["ACME Store #123"]
      },
      {
        "action": "AlignCenter",
        "args": []
      },
      {
        "action": "ContinueLine",
        "args": ["="]
      },
      {
        "action": "HeaderItem",
        "args": [
          [["Item", 15, 0], ["Qty", 8, 1], ["Price", 8, 2]]
        ]
      },
      {
        "action": "RowItem",
        "args": [
          ["SKU001", "Product Name", 2, 19.99],
          ["SKU002", "Another Item", 1, 29.99]
        ]
      },
      {
        "action": "Body",
        "args": [""]
      },
      {
        "action": "AlignRight",
        "args": []
      },
      {
        "action": "CustomItem",
        "args": [
          [["Subtotal: $49.98", 20, 2], ["Tax (8%): $4.00", 20, 2], ["Total: $53.98", 20, 2]]
        ]
      },
      {
        "action": "Footer",
        "args": ["Thank you for your purchase!", "Visit us again"]
      },
      {
        "action": "Feed",
        "args": [2]
      },
      {
        "action": "PaperCut",
        "args": [true]
      }
    ]
  }
}
```

**Supported Operations**:

| Operation | Args | Description |
|:----------|:-----|:------------|
| `Reset` | `[]` | Initialize printer |
| `Header` | `[text, ...]` | Print bold header text |
| `Footer` | `[text, ...]` | Print footer text |
| `Body` | `[text, ...]` | Print body text |
| `Text` | `[text]` | Print plain text |
| `HeaderItem` | `[[[text, width, align], ...]]` | Print columnar header (align: 0=left, 1=center, 2=right) |
| `RowItem` | `[[code, name, qty, price], ...]` | Print item rows with code, name, quantity, price |
| `CustomItem` | `[[[text, width, align], ...], ...]` | Print custom multi-line columnar rows |
| `PrintLogo` | `[logoKey]` | Print stored logo by resource key |
| `AlignLeft` | `[]` | Set alignment to left |
| `AlignCenter` | `[]` | Set alignment to center |
| `AlignRight` | `[]` | Set alignment to right |
| `Feed` | `[lines]` | Feed paper (default: 1 line) |
| `ContinueLine` | `[char]` | Print continuous line with character |
| `PaperCut` | `[full]` | Cut paper (full=true for full cut) |
| `Beep` | `[]` | Sound beeper |
| `OpenCashDrawer` | `[]` | Open cash drawer |

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Ticket processed successfully",
  "printerName": "Epson TM-T20II",
  "operationsProcessed": 12
}
```

---

### 6. **Get Logo Keys**

Retrieves all stored logo/image keys from the resource file.

```http
GET /api/printer/logo-keys
```

**Parameters**: None

**Response** (200 OK):
```json
[
  {
    "key": "logo_acme",
    "filename": "logo_acme.png",
    "contentType": "image/png"
  },
  {
    "key": "logo_company",
    "filename": "logo_company.jpg",
    "contentType": "image/jpeg"
  }
]
```

---

### 7. **Get Logo**

Downloads a previously stored logo by key.

```http
GET /api/printer/logo?key={logoKey}
```

**Query Parameters**:
| Parameter | Type | Required | Description |
|:----------|:-----|:---------|:------------|
| `key` | string | ✅ Yes | Logo resource key |

**cURL Example**:
```bash
curl -X GET "http://localhost:5075/api/printer/logo?key=logo_acme" \
  -o downloaded_logo.png
```

**Response** (200 OK):
- Binary image data with appropriate Content-Type header

**Error Responses**:
- `400 Bad Request`: Missing or invalid key
- `404 Not Found`: Logo key not found

---

### 8. **Save Logo**

Uploads and stores a logo/image file in the resource repository.

```http
PUT /api/printer/save_logo
Content-Type: multipart/form-data
```

**Form Parameters**:
| Parameter | Type | Required | Description |
|:----------|:-----|:---------|:------------|
| `logo` | file | ✅ Yes | Image file (PNG, JPEG, SVG, WebP), max 5 MB |
| `resourceKey` | string | ❌ No | Custom resource key (auto-generated if omitted) |

**Supported Content-Types**:
- `image/png`
- `image/jpeg`
- `image/svg+xml`
- `image/webp`

**cURL Example**:
```bash
curl -X PUT http://localhost:5075/api/printer/save_logo \
  -F "logo=@company_logo.png" \
  -F "resourceKey=logo_company"
```

**Response** (200 OK):
```json
{
  "key": "logo_company"
}
```

**Error Responses**:
- `400 Bad Request`: Invalid file, unsupported format, or file too large
- `500 Internal Server Error`: File storage error

---

## 🔧 Installation & Setup

### Prerequisites
- **.NET 10** runtime or SDK
- **Windows** (for Windows Service mode) or **Linux with CUPS** (for CUPS printer support)
- Network access to printers

### Installation

1. **Clone the repository**:
   ```bash
   git clone https://github.com/abyleyva/printspooljobservice.git
   cd printspooljobservice
   ```

2. **Build the project**:
   ```bash
   dotnet build -c Release
   ```

3. **Publish for deployment**:
   ```bash
   dotnet publish -c Release -o ./publish
   ```

4. **Run as Windows Service** (Windows only):
   ```powershell
   # Install
   sc create PrintSpoolJobService binPath="C:\path\to\PrintSpoolJobService.exe"

   # Start
   net start PrintSpoolJobService

   # Stop
   net stop PrintSpoolJobService
   ```

5. **Run as Console Application**:
   ```bash
   dotnet PrintSpoolJobService.dll
   ```

---

## ⚙️ Configuration

Edit `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Http": {
    "Port": 5075
  }
}
```

### Environment-Specific Settings

Create `appsettings.Production.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

---

## 🔒 Security Considerations

⚠️ **Important**: This service is designed for internal network use only.

- **No Authentication**: By default, endpoints are unauthenticated
- **CORS Enabled**: All origins allowed by default (configurable)
- **Input Validation**: Printer names and file types are validated
- **File Size Limits**: Enforced on uploads (PDF: 10MB, Labels: 2MB, Logos: 5MB)
- **Platform-Specific**: Printer access is restricted to locally connected printers

### Recommendations for Production

1. Deploy behind a secure reverse proxy (nginx, IIS)
2. Implement authentication/authorization
3. Restrict CORS origins
4. Use HTTPS
5. Implement rate limiting
6. Enable audit logging
7. Restrict network access (firewall rules)

---

## 📊 Logging

The service logs to:
- **Windows Event Log** (Windows Service mode) with source: `PrintSpoolJobService`
- **Console output** (Console/Development mode)
- **File** (configurable in appsettings.json)

### Log Levels

- `Information`: Normal operation events
- `Warning`: Non-critical issues
- `Error`: Operation failures
- `Debug`: Detailed operation flow (Development only)

---

## 🆘 Troubleshooting

### Printers Not Detected

**Windows**:
```powershell
# Verify printer installation
Get-Printer | Select-Object Name
```

**Linux**:
```bash
# Verify CUPS installation
lpstat -p -d
# Or check CUPS web interface
curl http://localhost:631/printers/
```

### Permission Errors

Ensure the user running the service has printer access permissions:
- **Windows**: Add user to local Administrators or Printer Operators group
- **Linux**: Add user to `lp` and `lpadmin` groups

### Port Already in Use

Change port in `appsettings.json`:
```json
{
  "Http": {
    "Port": 5076
  }
}
```

---

## 📝 API Response Codes

| Status | Code | Meaning |
|:-------|:-----|:--------|
| Success | `200` | Operation successful |
| Client Error | `400` | Bad request / invalid parameters |
| Not Found | `404` | Printer or resource not found |
| Not Implemented | `501` | Printer enumeration not available on platform |
| Server Error | `500` | Internal server error |
| Cancelled | `499` | Request cancelled by client |

---

## 🤝 Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

---

## 👨‍💻 Authors

- **[@abyleyva](https://www.github.com/abyleyva)** - Creator and Maintainer

---

## 📞 Support

For issues, questions, or suggestions:
- **GitHub Issues**: [Report a bug](https://github.com/abyleyva/printspooljobservice/issues)
- **GitHub Discussions**: [Ask a question](https://github.com/abyleyva/printspooljobservice/discussions)

---

## 🎯 Roadmap

- [ ] JWT authentication support
- [ ] Batch print operations
- [ ] Print queue management
- [ ] Webhook notifications
- [ ] Docker containerization
- [ ] Print job history and analytics

---

## ⭐ Show Your Support

Give a ⭐️ if this project helped you!





