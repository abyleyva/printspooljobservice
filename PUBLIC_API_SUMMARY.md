# Print Spool Job Service - Public API Summary

**Enterprise-Grade Printing Solution Built with .NET 10**

---

## 📊 Executive Summary

Print Spool Job Service is a professional RESTful API that provides unified management of diverse printing operations across networked printers. Designed for enterprise environments, it combines:

- **Multi-Format Support**: PDF, ZPL/EZPL Labels, Thermal Receipts
- **Cross-Platform**: Windows & Linux with CUPS support
- **Production Ready**: Windows Service hosting, comprehensive logging, error handling
- **Developer Friendly**: Swagger documentation, comprehensive API examples, OpenAPI specification

---

## 🎯 Key Capabilities

### Printer Management
✅ Automatic printer enumeration (Windows & CUPS)  
✅ Real-time printer discovery  
✅ Network IP detection (IPv4 & IPv6)  

### Document Printing
✅ PDF printing with validation  
✅ ZPL/EZPL label printing (Zebra printers)  
✅ Thermal receipt formatting  

### Advanced Features
✅ Logo/image storage and retrieval  
✅ Complex ticket layouts with alignment & wrapping  
✅ Base64 resource encoding  
✅ Multi-printer support  

### Enterprise Features
✅ Windows Service hosting  
✅ Comprehensive error handling  
✅ Request validation & size limits  
✅ CORS support  
✅ Production logging  

---

## 🚀 Quick Start

### Access the API

```bash
# Base URL
http://localhost:5075/api/printer

# Swagger Documentation (Development)
http://localhost:5075/swagger

# Example: List Printers
curl http://localhost:5075/api/printer/get-printers
```

### Print a PDF

```bash
curl -X POST http://localhost:5075/api/printer/print-pdf \
  -F "documentPDF=@invoice.pdf" \
  -F "printerName=Brother HL-L2350DW"
```

### Print a Receipt

```bash
curl -X POST http://localhost:5075/api/printer/print-ticket \
  -H "Content-Type: application/json" \
  -d '{
	"newTicket": {
	  "printerName": "Epson TM-T20II",
	  "operations": [
		{"action": "Reset", "args": []},
		{"action": "Header", "args": ["ACME Store"]},
		{"action": "RowItem", "args": [["SKU001", "Product", 1, 19.99]]},
		{"action": "PaperCut", "args": [true]}
	  ]
	}
  }'
```

---

## 📋 API Endpoints

| Method | Endpoint | Purpose |
|:-------|:---------|:--------|
| **GET** | `/get-printers` | List available printers |
| **GET** | `/get-local-ipaddress` | Get network IP addresses |
| **POST** | `/print-pdf` | Print PDF documents |
| **POST** | `/print-label` | Print ZPL/EZPL labels |
| **POST** | `/print-ticket` | Print thermal receipts |
| **GET** | `/logo-keys` | List stored logos |
| **GET** | `/logo?key={key}` | Download logo |
| **PUT** | `/save_logo` | Upload logo |

---

## 📐 Technical Specifications

**Framework**: .NET 10 ASP.NET Core  
**API Style**: REST with JSON payloads  
**Authentication**: None by default (configurable)  
**Rate Limiting**: None by default (add via reverse proxy)  
**Logging**: Event Log (Windows), Journal (Linux), Console  
**Hosting**: Windows Service, systemd, Docker, IIS, Kestrel  

---

## 🔒 Security Model

### Current State
- No built-in authentication
- CORS: All origins allowed
- File size limits enforced
- Input validation on printer names

### Production Recommendations
- Deploy behind reverse proxy (nginx/IIS)
- Implement JWT or API Key authentication
- Enable HTTPS/TLS
- Restrict CORS origins
- Implement rate limiting
- Enable audit logging
- Restrict network access

---

## 📦 Deployment Options

### Windows
- Standalone executable
- Windows Service (recommended)
- IIS application

### Linux
- Standalone with systemd
- Docker container
- Kubernetes deployment

### Cloud
- Azure App Service
- AWS Elastic Beanstalk
- Google Cloud Run
- Docker registry deployment

---

## 📊 Use Cases

### Retail & Point of Sale
Receipt printing, label generation, ticket processing

### Logistics & Shipping
Shipping label printing, barcode generation, tracking labels

### Healthcare
Patient labels, prescription printing, receipt generation

### Manufacturing
Work order printing, barcode/QR code labels, inventory labels

### Financial Services
Document printing, compliance reporting, receipt generation

---

## 🛠️ Configuration

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

---

## 📈 Performance Metrics

| Metric | Value | Notes |
|:-------|:------|:------|
| Request Timeout | 30s (configurable) | Per operation |
| Max PDF Size | 10 MB | Configurable |
| Max Label Size | 2 MB | Configurable |
| Max Logo Size | 5 MB | Configurable |
| Concurrent Printers | Unlimited | System dependent |
| Response Time | <500ms | Typical network operation |

---

## 🔧 Integration Examples

### Python
```python
import requests

response = requests.post(
	'http://localhost:5075/api/printer/print-pdf',
	files={'documentPDF': open('invoice.pdf', 'rb'),
		   'printerName': 'Brother HL-L2350DW'}
)
```

### JavaScript/Node.js
```javascript
const FormData = require('form-data');
const fs = require('fs');

const form = new FormData();
form.append('documentPDF', fs.createReadStream('invoice.pdf'));
form.append('printerName', 'Brother HL-L2350DW');

await axios.post('http://localhost:5075/api/printer/print-pdf', form);
```

### PowerShell
```powershell
Invoke-RestMethod -Uri 'http://localhost:5075/api/printer/get-printers' -Method Get
```

---

## 📚 Documentation Files

| File | Purpose |
|:-----|:--------|
| **README.md** | Complete API reference and features |
| **API_EXAMPLES.md** | Code examples (Python, JS, cURL, PowerShell) |
| **DEPLOYMENT.md** | Installation and deployment guide |
| **openapi.yaml** | OpenAPI/Swagger specification |

---

## 🆘 Support Resources

- **GitHub Repository**: https://github.com/abyleyva/printspooljobservice
- **Issue Tracker**: GitHub Issues
- **Documentation**: Built-in Swagger UI
- **Examples**: Comprehensive code samples included

---

## ✅ Quality Assurance

- ✓ Compiled and tested with .NET 10
- ✓ Cross-platform compatibility (Windows, Linux)
- ✓ Error handling for all endpoints
- ✓ Input validation and sanitization
- ✓ Request size limits enforced
- ✓ Comprehensive logging
- ✓ Production-ready code

---

## 📋 Checklist for Deployment

- [ ] Review and understand all endpoints
- [ ] Update appsettings.json for environment
- [ ] Configure printer access permissions
- [ ] Set up reverse proxy (production)
- [ ] Enable HTTPS/TLS (production)
- [ ] Configure authentication (production)
- [ ] Set up logging and monitoring
- [ ] Create backup strategy
- [ ] Document custom configurations
- [ ] Test all endpoints in target environment

---

## 🎓 Learning Path

1. **Start Here**: Read README.md for API overview
2. **Explore Examples**: Review API_EXAMPLES.md for your language
3. **Try It Out**: Use Swagger UI or cURL to test endpoints
4. **Deploy**: Follow DEPLOYMENT.md for your environment
5. **Integrate**: Implement in your application
6. **Monitor**: Set up logging and alerts

---

## 🔄 Version Information

| Component | Version |
|:----------|:--------|
| .NET Target | 10.0 |
| API Version | 1.0 |
| OpenAPI Version | 3.0.0 |
| Release Date | 2024 |

---

## 📞 Next Steps

1. **Clone Repository**: `git clone https://github.com/abyleyva/printspooljobservice.git`
2. **Build Project**: `dotnet build`
3. **Review Documentation**: Start with README.md
4. **Try Examples**: Execute API_EXAMPLES.md samples
5. **Deploy**: Follow DEPLOYMENT.md
6. **Monitor**: Check logs and test endpoints

---

## 📄 License

MIT License - Free for commercial and personal use  
See LICENSE file for details

---

## 👨‍💻 Author

**[@abyleyva](https://github.com/abyleyva)**  
Creator and Maintainer of Print Spool Job Service

---

**Last Updated**: 2024  
**Status**: Production Ready  
**Stability**: Stable
