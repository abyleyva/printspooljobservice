# Print Spool Job Service - Deployment Guide

Complete guide for deploying the Print Spool Job Service in different environments.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Windows Deployment](#windows-deployment)
3. [Linux Deployment](#linux-deployment)
4. [Docker Deployment](#docker-deployment)
5. [Configuration](#configuration)
6. [Security Hardening](#security-hardening)
7. [Monitoring & Logging](#monitoring--logging)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### System Requirements

| Component | Requirement |
|-----------|-------------|
| Runtime | .NET 10 Runtime or SDK |
| OS | Windows 7+ or Linux (Ubuntu 18.04+, CentOS 7+) |
| Memory | Minimum 512 MB, Recommended 1+ GB |
| Network | Network interface for printer connectivity |
| Disk Space | Minimum 500 MB for application and logs |

### Required Software

**Windows**:
- .NET 10 Runtime
- Windows Service utilities (included in OS)
- Optional: IIS for reverse proxy setup

**Linux**:
- .NET 10 Runtime
- CUPS (Common Unix Printing System)
- systemd for service management
- Optional: nginx/Apache for reverse proxy

### Network Access

- **Ports**: Service runs on port 5075 (configurable)
- **Printers**: Must be accessible from the same network
- **CORS**: Pre-configured for all origins (change in production)

---

## Windows Deployment

### Step 1: Install .NET Runtime

```powershell
# Download and install .NET 10 Runtime
# Visit: https://dotnet.microsoft.com/download/dotnet

# Verify installation
dotnet --version
```

### Step 2: Publish the Application

```powershell
# Build the application
dotnet publish -c Release -o C:\PrintSpoolJobService

# Verify files
Get-ChildItem C:\PrintSpoolJobService
```

### Step 3: Create Windows Service

#### Option A: Using sc.exe (Recommended)

```powershell
# Run PowerShell as Administrator

# Create service
sc.exe create PrintSpoolJobService `
	binPath= "C:\PrintSpoolJobService\PrintSpoolJobService.exe" `
	displayName= "Print Spool Job Service" `
	start= auto

# Set service description
sc.exe description PrintSpoolJobService "RESTful API for managing printer operations"

# Verify service was created
sc.exe query PrintSpoolJobService
```

#### Option B: Using New-Service (PowerShell 6+)

```powershell
# Requires Administrator and PowerShell 6+
New-Service -Name "PrintSpoolJobService" `
	-BinaryPathName "C:\PrintSpoolJobService\PrintSpoolJobService.exe" `
	-DisplayName "Print Spool Job Service" `
	-StartupType Automatic `
	-Description "RESTful API for managing printer operations"
```

### Step 4: Configure Permissions

```powershell
# Create service account (optional, more secure)
$password = ConvertTo-SecureString "StrongPassword123!" -AsPlainText -Force
New-LocalUser -Name "PrintService" -Password $password -Description "Service account for Print Spool Job Service"

# Add to Printer Operators group
Add-LocalGroupMember -Group "Print Operators" -Member "PrintService"

# Update service logon (via GUI or PowerShell)
# Services.msc -> Right-click service -> Properties -> Log On tab
```

### Step 5: Start Service

```powershell
# Start the service
Start-Service -Name PrintSpoolJobService

# Check service status
Get-Service PrintSpoolJobService

# View service logs
Get-EventLog -LogName "System" -Source "PrintSpoolJobService" -Newest 10
```

### Step 6: Verify Installation

```powershell
# Test API endpoint
Invoke-RestMethod -Uri "http://localhost:5075/api/printer/get-printers"

# Access Swagger UI (if in Development mode)
Start-Process "http://localhost:5075/swagger"
```

### Service Management Commands

```powershell
# Start service
Start-Service PrintSpoolJobService

# Stop service
Stop-Service PrintSpoolJobService

# Restart service
Restart-Service PrintSpoolJobService

# View status
Get-Service PrintSpoolJobService

# Remove service
sc.exe delete PrintSpoolJobService

# View recent logs
Get-EventLog -LogName "System" -Source "PrintSpoolJobService" -Newest 20 | 
	Select-Object TimeGenerated, EventID, Message
```

---

## Linux Deployment

### Step 1: Install .NET Runtime

**Ubuntu/Debian**:
```bash
# Add Microsoft package source
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10

# Add to PATH
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc

# Verify
dotnet --version
```

**CentOS/RHEL**:
```bash
# Install from Microsoft repositories
sudo rpm -Uvh https://dot.microsoft.com/dotnet/release-metadata/releases-index.json
sudo yum install -y dotnet-sdk-10

# Verify
dotnet --version
```

### Step 2: Install CUPS (Optional but Recommended)

```bash
# Ubuntu/Debian
sudo apt-get update
sudo apt-get install -y cups cups-client

# CentOS/RHEL
sudo yum install -y cups

# Start CUPS service
sudo systemctl start cups
sudo systemctl enable cups

# Verify CUPS
lpstat -p -d
```

### Step 3: Publish Application

```bash
# Build and publish
dotnet publish -c Release -o /opt/printspooljobservice

# Set permissions
sudo chown -R printspool:printspool /opt/printspooljobservice
sudo chmod +x /opt/printspooljobservice/PrintSpoolJobService

# Verify
ls -la /opt/printspooljobservice/
```

### Step 4: Create systemd Service

```bash
# Create service file
sudo nano /etc/systemd/system/printspooljobservice.service
```

Paste the following content:

```ini
[Unit]
Description=Print Spool Job Service
Documentation=https://github.com/abyleyva/printspooljobservice
After=network.target

[Service]
Type=notify
User=printspool
WorkingDirectory=/opt/printspooljobservice
ExecStart=/usr/bin/dotnet /opt/printspooljobservice/PrintSpoolJobService.dll
Restart=always
RestartSec=5
StandardOutput=journal
StandardError=journal

# Environment variables
Environment="ASPNETCORE_ENVIRONMENT=Production"
Environment="DOTNET_DiagnosticPorts=/tmp/printspool-diagnostic.sock"

# Resource limits
LimitNOFILE=65536
LimitNPROC=4096

# Security
PrivateTmp=yes
NoNewPrivileges=yes

[Install]
WantedBy=multi-user.target
```

```bash
# Enable service
sudo systemctl daemon-reload
sudo systemctl enable printspooljobservice
sudo systemctl start printspooljobservice

# Check status
sudo systemctl status printspooljobservice
```

### Step 5: Configure Printer Access

```bash
# Add user to lp and lpadmin groups
sudo usermod -aG lp printspool
sudo usermod -aG lpadmin printspool

# Verify printer access
sudo -u printspool lpstat -p -d
```

### Service Management Commands

```bash
# Start service
sudo systemctl start printspooljobservice

# Stop service
sudo systemctl stop printspooljobservice

# Restart service
sudo systemctl restart printspooljobservice

# Check status
sudo systemctl status printspooljobservice

# View logs
sudo journalctl -u printspooljobservice -f
sudo journalctl -u printspooljobservice -n 50

# Enable/disable auto-start
sudo systemctl enable printspooljobservice
sudo systemctl disable printspooljobservice
```

---

## Docker Deployment

### Step 1: Create Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:10

WORKDIR /app

# Install CUPS client libraries
RUN apt-get update && apt-get install -y \
	cups-client \
	libcups2 \
	&& rm -rf /var/lib/apt/lists/*

# Copy published application
COPY publish/ .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
	CMD curl -f http://localhost:5075/api/printer/get-printers || exit 1

# Expose port
EXPOSE 5075

# Run application
ENTRYPOINT ["dotnet", "PrintSpoolJobService.dll"]
```

### Step 2: Build Docker Image

```bash
# Publish application first
dotnet publish -c Release -o publish

# Build Docker image
docker build -t printspooljobservice:latest .

# Verify image
docker images | grep printspooljobservice
```

### Step 3: Run Docker Container

```bash
# Basic usage
docker run -d \
	-p 5075:5075 \
	--name printspool \
	printspooljobservice:latest

# With volume mounting (for persistent logo storage)
docker run -d \
	-p 5075:5075 \
	-v /var/lib/printspool/resources:/app/Resources \
	--name printspool \
	printspooljobservice:latest

# With network access to printers
docker run -d \
	-p 5075:5075 \
	--network host \
	--name printspool \
	printspooljobservice:latest

# With environment variables
docker run -d \
	-p 5075:5075 \
	-e "ASPNETCORE_ENVIRONMENT=Production" \
	-e "Http__Port=5075" \
	--name printspool \
	printspooljobservice:latest
```

### Docker Compose

Create `docker-compose.yml`:

```yaml
version: '3.8'

services:
  printspool:
	image: printspooljobservice:latest
	container_name: printspool-service
	ports:
	  - "5075:5075"
	volumes:
	  - printspool-data:/app/Resources
	environment:
	  - ASPNETCORE_ENVIRONMENT=Production
	  - Http__Port=5075
	restart: unless-stopped
	healthcheck:
	  test: ["CMD", "curl", "-f", "http://localhost:5075/api/printer/get-printers"]
	  interval: 30s
	  timeout: 3s
	  retries: 3
	networks:
	  - printer-network

networks:
  printer-network:
	driver: bridge

volumes:
  printspool-data:
	driver: local
```

```bash
# Start services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

---

## Configuration

### appsettings.json

```json
{
  "Logging": {
	"LogLevel": {
	  "Default": "Information",
	  "Microsoft.AspNetCore": "Warning",
	  "Microsoft.AspNetCore.Hosting": "Information"
	},
	"Console": {
	  "IncludeScopes": true
	}
  },
  "AllowedHosts": "*",
  "Http": {
	"Port": 5075
  }
}
```

### appsettings.Production.json

```json
{
  "Logging": {
	"LogLevel": {
	  "Default": "Warning",
	  "Microsoft.AspNetCore": "Warning"
	}
  },
  "AllowedHosts": "*.yourdomain.com",
  "Http": {
	"Port": 5075
  }
}
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Development | Environment (Development, Staging, Production) |
| `Http__Port` | 5075 | Listening port |
| `ASPNETCORE_URLS` | http://*:5075 | URLs to listen on |
| `DOTNET_DiagnosticPorts` | - | Diagnostic port for monitoring |

Set environment variables:

**Windows**:
```powershell
[System.Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")
```

**Linux**:
```bash
export ASPNETCORE_ENVIRONMENT=Production
```

---

## Security Hardening

### 1. Network Security

```nginx
# nginx reverse proxy with SSL
server {
	listen 443 ssl http2;
	server_name print-api.yourdomain.com;

	ssl_certificate /etc/letsencrypt/live/print-api.yourdomain.com/fullchain.pem;
	ssl_certificate_key /etc/letsencrypt/live/print-api.yourdomain.com/privkey.pem;

	ssl_protocols TLSv1.2 TLSv1.3;
	ssl_ciphers HIGH:!aNULL:!MD5;

	location / {
		proxy_pass http://localhost:5075;
		proxy_set_header Host $host;
		proxy_set_header X-Real-IP $remote_addr;
		proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
		proxy_set_header X-Forwarded-Proto $scheme;

		# Rate limiting
		limit_req zone=api burst=10 nodelay;
	}
}

# Rate limiting upstream
limit_req_zone $binary_remote_addr zone=api:10m rate=5r/s;
```

### 2. Firewall Configuration

**Windows**:
```powershell
# Allow port 5075 for specific IP range
New-NetFirewallRule -DisplayName "Print Service" `
	-Direction Inbound -Action Allow `
	-Protocol TCP -LocalPort 5075 `
	-RemoteAddress 192.168.1.0/24
```

**Linux**:
```bash
# Using UFW
sudo ufw allow from 192.168.1.0/24 to any port 5075

# Using firewalld
sudo firewall-cmd --add-rich-rule='rule family="ipv4" source address="192.168.1.0/24" port protocol="tcp" port="5075" accept' --permanent
```

### 3. Application Security

Update `Program.cs`:

```csharp
// Add authentication (JWT or API Key)
builder.Services.AddAuthentication("Bearer")
	.AddJwtBearer(options =>
	{
		options.Authority = "https://your-identity-server";
		options.Audience = "print-service";
	});

// Restrict CORS in production
builder.Services.AddCors(options =>
{
	options.AddPolicy("Production", policy =>
	{
		policy.WithOrigins("https://yourdomain.com")
			  .AllowAnyMethod()
			  .AllowAnyHeader();
	});
});

app.UseCors("Production");
```

---

## Monitoring & Logging

### Windows Event Log

```powershell
# View service logs
Get-EventLog -LogName "System" -Source "PrintSpoolJobService" | 
	Select-Object TimeGenerated, EntryType, Message | 
	Format-Table -AutoSize

# Export logs
Get-EventLog -LogName "System" -Source "PrintSpoolJobService" | 
	Export-Csv -Path "PrintServiceLogs.csv"
```

### Linux Journal

```bash
# View real-time logs
sudo journalctl -u printspooljobservice -f

# View last 100 lines
sudo journalctl -u printspooljobservice -n 100

# View since timestamp
sudo journalctl -u printspooljobservice --since "2024-01-01 10:00:00"

# Export to file
sudo journalctl -u printspooljobservice > printspool-logs.txt
```

### Application Insights Integration

```csharp
// Add Application Insights to Program.cs
builder.Services.AddApplicationInsightsTelemetry("InstrumentationKey");
```

---

## Troubleshooting

### Issue: Service won't start

**Windows**:
```powershell
# Check service status
Get-Service PrintSpoolJobService

# View event log
Get-EventLog -LogName "System" -Source "PrintSpoolJobService" -Newest 10

# Run application manually for debugging
C:\PrintSpoolJobService\PrintSpoolJobService.exe
```

**Linux**:
```bash
# Check service status
sudo systemctl status printspooljobservice

# View logs
sudo journalctl -u printspooljobservice -n 50

# Run application manually
cd /opt/printspooljobservice
dotnet PrintSpoolJobService.dll
```

### Issue: Port already in use

```powershell
# Windows - Find process using port
netstat -ano | findstr :5075
taskkill /PID <PID> /F

# Linux
sudo lsof -i :5075
sudo kill -9 <PID>

# Change port in appsettings.json
```

### Issue: Printers not detected

**Windows**:
```powershell
# Verify printer is installed
Get-Printer | Select-Object Name
```

**Linux**:
```bash
# Verify CUPS is running
sudo systemctl status cups

# List printers
lpstat -p -d

# Check CUPS web interface
curl http://localhost:631/printers/
```

### Issue: Permission denied

**Windows**:
```powershell
# Run service with Administrator privileges
# Services.msc -> Right-click -> Properties -> Log On tab
# Select "Local System account" or custom account with Admin rights
```

**Linux**:
```bash
# Add user to printer groups
sudo usermod -aG lp printspool
sudo usermod -aG lpadmin printspool

# Restart service
sudo systemctl restart printspooljobservice
```

---

## Performance Tuning

### Request Size Limits

Configured in the application:
- PDF: 10 MB max
- Labels: 2 MB max
- Logos: 5 MB max

### Connection Pooling

Adjust in appsettings.json:

```json
{
  "ConnectionStrings": {
	"MaxPoolSize": 100
  }
}
```

### Thread Pool

```csharp
// In Program.cs
ThreadPool.GetMinThreads(out int workerThreads, out int ioThreads);
ThreadPool.SetMinThreads(Math.Max(workerThreads, 50), ioThreads);
```

---

## Backup & Recovery

### Backup Strategy

```bash
# Linux backup script
#!/bin/bash
BACKUP_DIR="/backups/printspool"
mkdir -p $BACKUP_DIR

# Backup application
tar -czf $BACKUP_DIR/app-$(date +%Y%m%d).tar.gz /opt/printspooljobservice

# Backup resources (logos)
tar -czf $BACKUP_DIR/resources-$(date +%Y%m%d).tar.gz /opt/printspooljobservice/Resources

# Cleanup old backups (keep 30 days)
find $BACKUP_DIR -name "*.tar.gz" -mtime +30 -delete
```

### Restore

```bash
# Restore application
tar -xzf /backups/printspool/app-20240101.tar.gz -C /

# Restart service
sudo systemctl restart printspooljobservice
```

---

**Last Updated**: 2024
**Version**: 1.0
**Compatibility**: .NET 10+
