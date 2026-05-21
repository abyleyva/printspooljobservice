📋 **START HERE** - [QUICKSTART.md](QUICKSTART.md) | [DOCUMENTATION_INDEX.md](DOCUMENTATION_INDEX.md) | [PROJECT_SUMMARY.md](PROJECT_SUMMARY.md)

---

# Print Spool Job Service - Complete Documentation Suite

Welcome! This repository contains the **Print Spool Job Service**, a professional RESTful API for managing printing operations across networked printers, along with comprehensive documentation.

## 🎯 Choose Your Path

### 👨‍💼 **I'm a Manager/Executive**
→ Read: [PUBLIC_API_SUMMARY.md](PUBLIC_API_SUMMARY.md) (5 min)
- Executive overview
- Key capabilities
- Use cases
- ROI information

### 👨‍💻 **I'm a Developer**
→ Start: [QUICKSTART.md](QUICKSTART.md) (5 min) → [README.md](README.md) (30 min) → [API_EXAMPLES.md](API_EXAMPLES.md)
- Complete API reference
- Code examples (Python, JavaScript, PowerShell, cURL)
- Integration patterns
- Response formats

### 🔧 **I'm DevOps/System Admin**
→ Start: [QUICKSTART.md](QUICKSTART.md) (5 min) → [DEPLOYMENT.md](DEPLOYMENT.md) (45 min)
- Step-by-step installation
- Windows & Linux setup
- Docker deployment
- Configuration guide

### 🔒 **I'm a Security Officer**
→ Read: [SECURITY.md](SECURITY.md) (60 min) → [DEPLOYMENT.md](DEPLOYMENT.md) - Security section
- Authentication options
- Network security
- Encryption strategies
- Compliance guidelines

---

## 📚 Documentation Files

| File | Purpose | Length | Audience |
|:-----|:--------|:-------|:---------|
| **[QUICKSTART.md](QUICKSTART.md)** | Get running in 5 minutes | 2 pages | Everyone |
| **[PUBLIC_API_SUMMARY.md](PUBLIC_API_SUMMARY.md)** | Executive overview | 3 pages | Managers |
| **[README.md](README.md)** | Complete API reference | 25 pages | Developers |
| **[API_EXAMPLES.md](API_EXAMPLES.md)** | Code examples (4 languages) | 30 pages | Developers |
| **[DEPLOYMENT.md](DEPLOYMENT.md)** | Deployment & setup guide | 35 pages | DevOps |
| **[SECURITY.md](SECURITY.md)** | Security implementation | 40 pages | Security |
| **[DOCUMENTATION_INDEX.md](DOCUMENTATION_INDEX.md)** | Navigation & learning paths | 15 pages | Everyone |
| **[openapi.yaml](openapi.yaml)** | OpenAPI specification | - | Developers |

---

## 🚀 Quick Start (2 Minutes)

### Install & Run

**Windows**:
```powershell
dotnet publish -c Release -o C:\PrintSpoolJobService
sc.exe create PrintSpoolJobService binPath= "C:\PrintSpoolJobService\PrintSpoolJobService.exe"
Start-Service PrintSpoolJobService
```

**Linux**:
```bash
dotnet publish -c Release -o /opt/printspooljobservice
sudo systemctl start printspooljobservice
```

**Docker**:
```bash
docker run -d -p 5075:5075 printspooljobservice:latest
```

### Test the API

```bash
# List printers
curl http://localhost:5075/api/printer/get-printers

# Print a PDF
curl -X POST http://localhost:5075/api/printer/print-pdf \
  -F "documentPDF=@file.pdf" \
  -F "printerName=MyPrinter"
```

---

## ✨ Key Features

✅ **Multi-Format Printing**
- PDF documents with validation
- ZPL/EZPL labels (Zebra printers)
- Thermal receipts with advanced formatting

✅ **Cross-Platform**
- Windows (Service, IIS, Console)
- Linux (systemd, Docker, Kubernetes)
- Cloud (Azure, AWS, GCP)

✅ **Enterprise Ready**
- Comprehensive logging
- Error handling & validation
- Request size limits
- CORS support
- Windows Service hosting

✅ **Fully Documented**
- 8 API endpoints
- 24+ code examples
- 4 programming languages
- Complete deployment guides
- Security best practices

---

## 📡 API Endpoints

| Method | Endpoint | Purpose |
|:-------|:---------|:--------|
| GET | `/get-printers` | List available printers |
| GET | `/get-local-ipaddress` | Network IP detection |
| POST | `/print-pdf` | Print PDF documents |
| POST | `/print-label` | Print ZPL/EZPL labels |
| POST | `/print-ticket` | Print thermal receipts |
| GET | `/logo-keys` | List stored logos |
| GET | `/logo?key={key}` | Download logo |
| PUT | `/save_logo` | Upload logo |

---

## 💻 Code Examples

### Python
```python
import requests

response = requests.post(
	'http://localhost:5075/api/printer/print-pdf',
	files={'documentPDF': open('file.pdf', 'rb'),
		   'printerName': 'MyPrinter'}
)
```

### JavaScript
```javascript
const FormData = require('form-data');

const form = new FormData();
form.append('documentPDF', fs.createReadStream('file.pdf'));
form.append('printerName', 'MyPrinter');

await axios.post('http://localhost:5075/api/printer/print-pdf', form);
```

### PowerShell
```powershell
Invoke-RestMethod -Uri 'http://localhost:5075/api/printer/get-printers' -Method Get
```

### cURL
```bash
curl -X POST http://localhost:5075/api/printer/print-pdf \
  -F "documentPDF=@file.pdf" \
  -F "printerName=MyPrinter"
```

---

## 🎓 Learning Paths

### Path 1: Developer (Integration)
1. [QUICKSTART.md](QUICKSTART.md) - 5 min
2. [README.md](README.md) - API Reference - 15 min
3. [API_EXAMPLES.md](API_EXAMPLES.md) - Your language - 10 min
**Total: 30 minutes**

### Path 2: DevOps (Deployment)
1. [QUICKSTART.md](QUICKSTART.md) - 5 min
2. [DEPLOYMENT.md](DEPLOYMENT.md) - Your platform - 30 min
3. [SECURITY.md](SECURITY.md) - Hardening - 20 min
**Total: 55 minutes**

### Path 3: Architect (Overview)
1. [PUBLIC_API_SUMMARY.md](PUBLIC_API_SUMMARY.md) - 5 min
2. [README.md](README.md) - Features - 15 min
3. [DEPLOYMENT.md](DEPLOYMENT.md) - All platforms - 20 min
4. [SECURITY.md](SECURITY.md) - Security model - 20 min
**Total: 60 minutes**

---

## 🔒 Security

The service includes:
- ✅ Input validation & sanitization
- ✅ File type verification
- ✅ Request size limits
- ✅ Error handling without information disclosure
- ✅ Thread-safe operations

For production, implement:
- ✅ HTTPS/TLS (via reverse proxy)
- ✅ Authentication (JWT/API Key)
- ✅ Rate limiting
- ✅ Firewall rules
- ✅ Comprehensive logging

See [SECURITY.md](SECURITY.md) for detailed implementation.

---

## 📦 Deployment Options

### Windows
- Windows Service (recommended)
- IIS Application
- Console Application

### Linux
- systemd Service (recommended)
- Docker Container
- Kubernetes Pod

### Cloud
- Azure App Service
- AWS Elastic Beanstalk
- Google Cloud Run
- Docker Registry

---

## 🛠️ Configuration

Edit `appsettings.json`:

```json
{
  "Logging": {
	"LogLevel": {
	  "Default": "Information"
	}
  },
  "AllowedHosts": "*",
  "Http": {
	"Port": 5075
  }
}
```

---

## 📞 Support & Resources

- **GitHub**: https://github.com/abyleyva/printspooljobservice
- **Issues**: GitHub Issues tab
- **Documentation**: All files in this repository
- **Examples**: [API_EXAMPLES.md](API_EXAMPLES.md)

---

## 🆘 Troubleshooting

**Service won't start?**
→ See [DEPLOYMENT.md - Troubleshooting](DEPLOYMENT.md#troubleshooting)

**API not responding?**
→ See [README.md - Troubleshooting](README.md#-troubleshooting)

**How to secure?**
→ See [SECURITY.md](SECURITY.md)

**Need code examples?**
→ See [API_EXAMPLES.md](API_EXAMPLES.md)

---

## ✅ Quality Assurance

- ✓ .NET 10 Compatible
- ✓ Cross-platform (Windows, Linux)
- ✓ Production-ready code
- ✓ Comprehensive error handling
- ✓ Full documentation
- ✓ Multiple deployment options
- ✓ Security best practices

---

## 📋 Quick Links

- 📖 **API Documentation**: [README.md](README.md)
- 💻 **Code Examples**: [API_EXAMPLES.md](API_EXAMPLES.md)
- 🚀 **Quick Start**: [QUICKSTART.md](QUICKSTART.md)
- 🏗️ **Deployment**: [DEPLOYMENT.md](DEPLOYMENT.md)
- 🔒 **Security**: [SECURITY.md](SECURITY.md)
- 📚 **Navigation**: [DOCUMENTATION_INDEX.md](DOCUMENTATION_INDEX.md)
- 📊 **Project Summary**: [PROJECT_SUMMARY.md](PROJECT_SUMMARY.md)
- 🔌 **OpenAPI Spec**: [openapi.yaml](openapi.yaml)

---

## 📄 License

MIT License - Free for commercial and personal use.

---

## 👨‍💻 Author

**[@abyleyva](https://github.com/abyleyva)** - Creator and Maintainer

---

**Status**: Production Ready ✅  
**Version**: 1.0  
**Last Updated**: 2024

---

**👉 Next Step**: Choose your role above and start with the recommended documentation!
