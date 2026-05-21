# 🚀 Print Spool Job Service - Quick Start Guide

**Get started with the Print Spool Job Service in 5 minutes**

---

## 📦 Installation (Choose Your Platform)

### Windows (Quick Start)

```powershell
# 1. Publish the application
dotnet publish -c Release -o C:\PrintSpoolJobService

# 2. Create Windows Service
sc.exe create PrintSpoolJobService `
	binPath= "C:\PrintSpoolJobService\PrintSpoolJobService.exe"

# 3. Start the service
Start-Service PrintSpoolJobService

# 4. Verify it's running
Invoke-RestMethod -Uri "http://localhost:5075/api/printer/get-printers"
```

### Linux (Quick Start)

```bash
# 1. Publish the application
dotnet publish -c Release -o /opt/printspooljobservice

# 2. Create systemd service file
sudo nano /etc/systemd/system/printspooljobservice.service
```

Paste this content:
```ini
[Unit]
Description=Print Spool Job Service
After=network.target

[Service]
Type=simple
User=root
WorkingDirectory=/opt/printspooljobservice
ExecStart=/usr/bin/dotnet /opt/printspooljobservice/PrintSpoolJobService.dll

[Install]
WantedBy=multi-user.target
```

```bash
# 3. Enable and start
sudo systemctl daemon-reload
sudo systemctl enable printspooljobservice
sudo systemctl start printspooljobservice

# 4. Verify it's running
curl http://localhost:5075/api/printer/get-printers
```

---

## 🧪 Test the API (Quick Examples)

### 1. List Available Printers

```bash
# cURL
curl http://localhost:5075/api/printer/get-printers

# PowerShell
Invoke-RestMethod -Uri "http://localhost:5075/api/printer/get-printers" -Method Get

# Python
import requests
response = requests.get("http://localhost:5075/api/printer/get-printers")
print(response.json())
```

### 2. Get Network IP Address

```bash
curl "http://localhost:5075/api/printer/get-local-ipaddress?select=ipv4"
```

### 3. Print a PDF

```bash
curl -X POST http://localhost:5075/api/printer/print-pdf \
  -F "documentPDF=@myfile.pdf" \
  -F "printerName=Brother HL-L2350DW"
```

### 4. Print a Thermal Receipt

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

## 📚 Documentation Reference

| Document | Purpose | Read Time |
|:---------|:--------|:----------|
| **[PUBLIC_API_SUMMARY.md](PUBLIC_API_SUMMARY.md)** | Quick overview | 5 min |
| **[README.md](README.md)** | Complete API reference | 30 min |
| **[API_EXAMPLES.md](API_EXAMPLES.md)** | Code examples | 20 min |
| **[DEPLOYMENT.md](DEPLOYMENT.md)** | Deployment guide | 45 min |
| **[SECURITY.md](SECURITY.md)** | Security guide | 60 min |
| **[DOCUMENTATION_INDEX.md](DOCUMENTATION_INDEX.md)** | Navigation guide | 10 min |

---

## 🔧 Configuration

Edit `appsettings.json`:

```json
{
  "Logging": {
	"LogLevel": {
	  "Default": "Information"
	}
  },
  "Http": {
	"Port": 5075
  }
}
```

---

## 🌐 Access the API

- **Base URL**: `http://localhost:5075/api/printer`
- **Swagger UI**: `http://localhost:5075/swagger` (Development mode)
- **Documentation**: See links above

---

## ✅ Verify Installation

### Check Service Status

**Windows**:
```powershell
Get-Service PrintSpoolJobService
```

**Linux**:
```bash
sudo systemctl status printspooljobservice
```

### Check Printers

```bash
curl http://localhost:5075/api/printer/get-printers
```

### Check Network IP

```bash
curl "http://localhost:5075/api/printer/get-local-ipaddress?select=ipv4"
```

---

## 🆘 Troubleshooting

### Service won't start

**Windows**:
```powershell
# Check if .NET is installed
dotnet --version

# Check event log
Get-EventLog -LogName "System" -Source "PrintSpoolJobService" -Newest 5
```

**Linux**:
```bash
# Check logs
sudo journalctl -u printspooljobservice -n 20

# Check if .NET is installed
dotnet --version
```

### Port already in use

Change port in `appsettings.json`:
```json
{
  "Http": {
	"Port": 5076
  }
}
```

### Printers not showing up

**Windows**:
```powershell
Get-Printer | Select-Object Name
```

**Linux**:
```bash
lpstat -p -d
```

---

## 🚀 Next Steps

### For Developers:
1. Read: [README.md - API Reference](README.md#api-reference)
2. Copy: [API_EXAMPLES.md](API_EXAMPLES.md) code samples
3. Integrate: Use examples in your application

### For DevOps:
1. Follow: [DEPLOYMENT.md](DEPLOYMENT.md) step-by-step
2. Configure: [appsettings.json](appsettings.json)
3. Secure: [SECURITY.md](SECURITY.md) hardening guide

### For Security:
1. Review: [SECURITY.md](SECURITY.md) completely
2. Implement: Authentication from [SECURITY.md](SECURITY.md#authentication--authorization)
3. Monitor: Logging from [DEPLOYMENT.md - Monitoring](DEPLOYMENT.md#monitoring--logging)

---

## 📞 Support

- **GitHub**: https://github.com/abyleyva/printspooljobservice
- **Issues**: GitHub Issues tab
- **Documentation**: All `.md` files in repository

---

## 🎯 Common Tasks

### Print a PDF (Python)

```python
import requests

response = requests.post(
	'http://localhost:5075/api/printer/print-pdf',
	files={
		'documentPDF': open('document.pdf', 'rb'),
		'printerName': 'Brother HL-L2350DW'
	}
)
print(response.json())
```

### Print a Label (JavaScript)

```javascript
const FormData = require('form-data');
const fs = require('fs');
const axios = require('axios');

const form = new FormData();
form.append('documentEZPL', fs.createReadStream('label.txt'));
form.append('printerName', 'Zebra GX430t');

await axios.post('http://localhost:5075/api/printer/print-label', form);
```

### Print a Receipt (PowerShell)

```powershell
$body = @{
	newTicket = @{
		printerName = "Epson TM-T20II"
		operations = @(
			@{ action = "Reset"; args = @() },
			@{ action = "Header"; args = @("Store") },
			@{ action = "PaperCut"; args = @($true) }
		)
	}
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri 'http://localhost:5075/api/printer/print-ticket' `
	-Method Post -ContentType 'application/json' -Body $body
```

---

## 📋 Checklist

- [ ] Service installed and running
- [ ] Port 5075 is accessible
- [ ] Printers detected by service
- [ ] API endpoints responding
- [ ] Test print successful
- [ ] Documentation reviewed
- [ ] Configuration customized
- [ ] Ready for production

---

**Status**: Ready to Use ✅  
**Version**: 1.0  
**Last Updated**: 2024

For detailed information, see [DOCUMENTATION_INDEX.md](DOCUMENTATION_INDEX.md)
