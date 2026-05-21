# Print Spool Job Service - Security & Best Practices Guide

Comprehensive security guidelines and best practices for the Print Spool Job Service.

---

## Table of Contents

1. [Security Architecture](#security-architecture)
2. [Network Security](#network-security)
3. [Authentication & Authorization](#authentication--authorization)
4. [Input Validation](#input-validation)
5. [Data Protection](#data-protection)
6. [Error Handling](#error-handling)
7. [Logging & Monitoring](#logging--monitoring)
8. [Deployment Security](#deployment-security)
9. [Incident Response](#incident-response)
10. [Compliance](#compliance)

---

## Security Architecture

### Current Implementation

The service includes built-in security measures:

1. **File Validation**
   - PDF header verification (magic bytes)
   - Content-Type validation
   - File size limits enforced

2. **Input Validation**
   - Printer name sanitization
   - Resource key validation (regex)
   - Request body parsing with error handling

3. **Resource Protection**
   - Thread-safe resource file access
   - Atomic file operations
   - Backup creation before updates

4. **Error Handling**
   - Graceful error responses
   - Information disclosure prevention
   - Consistent error formats

### Default Security Posture

⚠️ **Important**: The service is designed for **internal network use only**

- **No Authentication**: All endpoints are unauthenticated
- **No Rate Limiting**: Rate limiting should be implemented externally
- **CORS Enabled**: All origins allowed (configure for production)
- **HTTP Only**: No TLS by default (use reverse proxy for HTTPS)

---

## Network Security

### 1. Firewall Configuration

**Windows Firewall**:
```powershell
# Allow only specific IP range
New-NetFirewallRule -DisplayName "Print Service" `
	-Direction Inbound -Action Allow `
	-Protocol TCP -LocalPort 5075 `
	-RemoteAddress 192.168.1.0/24

# Allow only local network
New-NetFirewallRule -DisplayName "Print Service Local" `
	-Direction Inbound -Action Allow `
	-Protocol TCP -LocalPort 5075 `
	-RemoteAddress LocalSubnet
```

**Linux UFW**:
```bash
# Allow specific subnet
sudo ufw allow from 192.168.1.0/24 to any port 5075

# Allow only from specific host
sudo ufw allow from 192.168.1.100 to any port 5075

# Verify rules
sudo ufw status
```

### 2. Reverse Proxy with HTTPS

**nginx Configuration**:
```nginx
# /etc/nginx/sites-available/print-api
upstream print_service {
	server localhost:5075;
}

# HTTP to HTTPS redirect
server {
	listen 80;
	server_name print-api.yourdomain.com;
	return 301 https://$server_name$request_uri;
}

# HTTPS server
server {
	listen 443 ssl http2;
	server_name print-api.yourdomain.com;

	# SSL certificates (use Let's Encrypt)
	ssl_certificate /etc/letsencrypt/live/print-api.yourdomain.com/fullchain.pem;
	ssl_certificate_key /etc/letsencrypt/live/print-api.yourdomain.com/privkey.pem;

	# Modern TLS configuration
	ssl_protocols TLSv1.2 TLSv1.3;
	ssl_prefer_server_ciphers on;
	ssl_ciphers ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256;
	ssl_session_cache shared:SSL:10m;
	ssl_session_timeout 10m;

	# Security headers
	add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
	add_header X-Frame-Options "SAMEORIGIN" always;
	add_header X-Content-Type-Options "nosniff" always;
	add_header X-XSS-Protection "1; mode=block" always;
	add_header Referrer-Policy "strict-origin-when-cross-origin" always;

	# Rate limiting
	limit_req_zone $binary_remote_addr zone=api:10m rate=10r/s;
	limit_req zone=api burst=20 nodelay;

	location / {
		proxy_pass http://print_service;
		proxy_set_header Host $host;
		proxy_set_header X-Real-IP $remote_addr;
		proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
		proxy_set_header X-Forwarded-Proto $scheme;

		# Timeouts
		proxy_connect_timeout 60s;
		proxy_send_timeout 60s;
		proxy_read_timeout 60s;

		# Buffer settings
		proxy_buffering on;
		proxy_buffer_size 4k;
		proxy_buffers 8 4k;
	}
}
```

**IIS Configuration**:
```xml
<!-- web.config for IIS reverse proxy -->
<?xml version="1.0" encoding="utf-8"?>
<configuration>
	<system.webServer>
		<rewrite>
			<rules>
				<rule name="ReverseProxyPrintService" stopProcessing="true">
					<match url="api/printer/(.*)" />
					<conditions>
						<add input="{REQUEST_METHOD}" pattern="^(GET|POST|PUT|DELETE)$" />
					</conditions>
					<action type="Rewrite" url="http://localhost:5075/api/printer/{R:1}" />
				</rule>
			</rules>
		</rewrite>
		<httpProtocol>
			<customHeaders>
				<add name="Strict-Transport-Security" value="max-age=31536000; includeSubDomains" />
				<add name="X-Content-Type-Options" value="nosniff" />
				<add name="X-Frame-Options" value="SAMEORIGIN" />
			</customHeaders>
		</httpProtocol>
	</system.webServer>
</configuration>
```

### 3. Network Segmentation

```plaintext
┌─────────────────────────────────────────────┐
│         Internet / Untrusted Network         │
└─────────────────────────────────────────────┘
					  ↓
┌─────────────────────────────────────────────┐
│     Reverse Proxy (nginx/IIS) - DMZ        │
│              Port 443 (HTTPS)               │
└─────────────────────────────────────────────┘
					  ↓
┌─────────────────────────────────────────────┐
│   Internal Network (VPN/Private Network)    │
│  Print Service + Printer Network Segment    │
│              Port 5075 (HTTP)               │
└─────────────────────────────────────────────┘
					  ↓
┌─────────────────────────────────────────────┐
│   Printer Network (Isolated or Secure)      │
│        Local & Network Printers             │
└─────────────────────────────────────────────┘
```

---

## Authentication & Authorization

### Option 1: JWT (JSON Web Tokens)

Add JWT authentication to `Program.cs`:

```csharp
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];

builder.Services.AddAuthentication("Bearer")
	.AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(
				Encoding.UTF8.GetBytes(secretKey ?? "")),
			ValidateIssuer = true,
			ValidIssuer = issuer,
			ValidateAudience = true,
			ValidAudience = audience,
			ValidateLifetime = true,
			ClockSkew = TimeSpan.Zero
		};
	});

builder.Services.AddAuthorizationBuilder()
	.AddPolicy("PrinterAccess", policy =>
		policy.RequireAuthenticatedUser()
			  .RequireClaim("scope", "print:submit"));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
```

`appsettings.json`:
```json
{
  "JwtSettings": {
	"SecretKey": "your-super-secret-key-min-32-characters-long",
	"Issuer": "print-service",
	"Audience": "print-api-clients",
	"ExpirationMinutes": 60
  }
}
```

Add authorization to controller:

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "PrinterAccess")]
public class PrinterController : ControllerBase
{
	[HttpPost("print-pdf")]
	[Authorize]
	public async Task<IActionResult> PrintPDF(...)
	{
		// Method implementation
	}
}
```

### Option 2: API Key

```csharp
// Middleware for API Key validation
public class ApiKeyMiddleware
{
	private readonly RequestDelegate _next;
	private const string APIKEYHEADER = "X-API-Key";

	public ApiKeyMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context, IConfiguration config)
	{
		if (!context.Request.Headers.TryGetValue(APIKEYHEADER, out var extractedApiKey))
		{
			context.Response.StatusCode = 401;
			await context.Response.WriteAsync("API Key was not provided");
			return;
		}

		var apiKey = config.GetValue<string>("ApiKey");
		if (!apiKey.Equals(extractedApiKey))
		{
			context.Response.StatusCode = 401;
			await context.Response.WriteAsync("Invalid API Key");
			return;
		}

		await _next(context);
	}
}

// Add to Program.cs
app.UseMiddleware<ApiKeyMiddleware>();
```

### Option 3: Azure AD / Entra ID

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.Authority = "https://login.microsoftonline.com/{tenant-id}/v2.0";
		options.Audience = "api://print-service";
		options.TokenValidationParameters.ValidateLifetime = true;
	});
```

---

## Input Validation

### Current Implementation

The service validates:
- **File types**: PDF, Text, Image formats
- **File sizes**: 10MB (PDF), 2MB (Labels), 5MB (Logos)
- **Printer names**: No control characters, max 256 chars
- **Resource keys**: Alphanumeric, dots, underscores, hyphens
- **Content-Types**: Strict MIME type checking

### Additional Validation Recommendations

```csharp
// Add to PrinterController
private static class ValidationRules
{
	// Printer name: alphanumeric, spaces, hyphens only
	private static readonly Regex PrinterNameRegex = 
		new(@"^[a-zA-Z0-9\s\-]{1,256}$", RegexOptions.Compiled);

	// Resource key: alphanumeric, dots, underscores, hyphens
	private static readonly Regex ResourceKeyRegex = 
		new(@"^[a-zA-Z0-9._\-]{1,200}$", RegexOptions.Compiled);

	// JSON size limit
	private const long MaxJsonSize = 1_000_000; // 1 MB

	public static bool IsValidPrinterName(string name) =>
		!string.IsNullOrWhiteSpace(name) && 
		PrinterNameRegex.IsMatch(name);

	public static bool IsValidResourceKey(string key) =>
		!string.IsNullOrWhiteSpace(key) && 
		ResourceKeyRegex.IsMatch(key);
}
```

---

## Data Protection

### 1. Encryption at Rest

**Windows DPAPI**:
```csharp
using System.Security.Cryptography;

public static class DataProtection
{
	public static byte[] EncryptData(byte[] data)
	{
		return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
	}

	public static byte[] DecryptData(byte[] encryptedData)
	{
		return ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
	}
}
```

### 2. Secure Logo Storage

```csharp
// Store logos with encryption
[HttpPut("save_logo")]
public async Task<IActionResult> SaveLogo(IFormFile? logo, [FromForm] string? resourceKey)
{
	// ... validation ...

	var bytes = await ReadFileAsync(logo);

	// Encrypt sensitive data
	var encrypted = DataProtection.EncryptData(bytes);

	// Store encrypted data
	StoreResource(resourceKey, encrypted);

	return Ok(new { key = resourceKey });
}
```

### 3. Transport Security

```csharp
// Enforce HTTPS
public void Configure(IApplicationBuilder app)
{
	// Redirect HTTP to HTTPS
	app.UseHttpsRedirection();

	// HSTS (HTTP Strict Transport Security)
	app.UseHsts();
}

// In appsettings.json
{
  "Kestrel": {
	"Endpoints": {
	  "https": {
		"Url": "https://*:443",
		"Certificate": {
		  "Path": "/path/to/certificate.pfx",
		  "Password": "certificate-password"
		}
	  }
	}
  }
}
```

---

## Error Handling

### Secure Error Responses

❌ **Bad - Information Disclosure**:
```json
{
  "error": "SQLException: Login failed for user 'sa'",
  "stackTrace": "at PrintSpoolJobService.Controllers..."
}
```

✅ **Good - Safe Error**:
```json
{
  "error": "An error occurred while processing your request",
  "requestId": "0HO0FIB2HFEG0:00000001"
}
```

### Implementation

```csharp
// Global exception handler middleware
public class ExceptionHandlerMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<ExceptionHandlerMiddleware> _logger;

	public async Task InvokeAsync(HttpContext context)
	{
		try
		{
			await _next(context);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);

			context.Response.ContentType = "application/json";
			context.Response.StatusCode = StatusCodes.Status500InternalServerError;

			// Don't expose internal details
			var response = new
			{
				error = "An error occurred while processing your request",
				requestId = context.TraceIdentifier
			};

			await context.Response.WriteAsJsonAsync(response);
		}
	}
}

// Register in Program.cs
app.UseMiddleware<ExceptionHandlerMiddleware>();
```

---

## Logging & Monitoring

### 1. Comprehensive Logging

```json
{
  "Logging": {
	"LogLevel": {
	  "Default": "Information",
	  "Microsoft.AspNetCore": "Warning",
	  "PrintSpoolJobService": "Information"
	},
	"Console": {
	  "IncludeScopes": true
	},
	"EventLog": {
	  "LogLevel": {
		"Default": "Warning",
		"PrintSpoolJobService": "Information"
	  }
	},
	"File": {
	  "Path": "/var/log/printspool/service.log",
	  "MaxFileSize": 10485760,
	  "MaxRetainedFiles": 10
	}
  }
}
```

### 2. Audit Logging

```csharp
public interface IAuditLogger
{
	Task LogPrintJobAsync(string printerName, string documentType, string userId);
	Task LogAuthenticationAsync(string username, bool success);
	Task LogResourceAccessAsync(string resourceKey, string action, string userId);
}

public class AuditLogger : IAuditLogger
{
	private readonly ILogger<AuditLogger> _logger;

	public async Task LogPrintJobAsync(string printerName, string documentType, string userId)
	{
		_logger.LogInformation(
			"AUDIT: Print job submitted. Printer={Printer}, Type={Type}, User={User}, Timestamp={Timestamp}",
			printerName, documentType, userId, DateTime.UtcNow);

		// Store in database/audit log
		await Task.CompletedTask;
	}
}
```

### 3. Monitoring Metrics

```csharp
// Use Application Insights or Prometheus
builder.Services.AddApplicationInsightsTelemetry("InstrumentationKey");

// Custom metrics
public class PrintMetrics
{
	public static void RecordPrintJob(string printerName, string documentType)
	{
		// Increment counter
		// Record document size
		// Record print duration
	}
}
```

---

## Deployment Security

### 1. Service Account (Least Privilege)

**Windows**:
```powershell
# Create service account
$password = ConvertTo-SecureString "ComplexPassword123!" -AsPlainText -Force
New-LocalUser -Name "PrintService" -Password $password -PasswordNeverExpires

# Add minimal permissions
Add-LocalGroupMember -Group "Print Operators" -Member "PrintService"

# Create service with account
sc.exe create PrintSpoolJobService `
	binPath= "C:\PrintSpoolJobService\PrintSpoolJobService.exe" `
	obj= ".\PrintService"
```

**Linux**:
```bash
# Create service account
sudo useradd -r -s /bin/false printspool

# Set permissions
sudo chown -R printspool:printspool /opt/printspooljobservice
sudo chmod 750 /opt/printspooljobservice

# Configure sudo for service restart (if needed)
echo "printspool ALL=(ALL) NOPASSWD: /bin/systemctl restart printspooljobservice" | sudo tee /etc/sudoers.d/printspool
```

### 2. Secret Management

**Environment Variables**:
```bash
# Avoid hardcoding secrets
export API_KEY="your-secret-key"
export JWT_SECRET="your-jwt-secret"
export DB_CONNECTIONSTRING="secure-connection-string"
```

**Azure Key Vault**:
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault
var keyVaultUrl = new Uri($"https://{keyVaultName}.vault.azure.net/");
var credential = new DefaultAzureCredential();
builder.Configuration.AddAzureKeyVault(keyVaultUrl, credential);

app.Run();
```

### 3. Container Security (Docker)

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:10 AS runtime

# Run as non-root user
RUN useradd -m -u 1000 appuser

WORKDIR /app
COPY --chown=appuser:appuser publish/ .

# Read-only root filesystem
RUN chmod 755 /app && chmod 755 /app/*

USER appuser

EXPOSE 5075

# Security options
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
	CMD curl -f http://localhost:5075/api/printer/get-printers || exit 1

ENTRYPOINT ["dotnet", "PrintSpoolJobService.dll"]
```

Docker Compose with security:

```yaml
services:
  printspool:
	image: printspooljobservice:latest
	security_opt:
	  - no-new-privileges:true
	read_only: true
	tmpfs:
	  - /tmp
	volumes:
	  - printspool-data:/app/Resources:rw
	environment:
	  - ASPNETCORE_ENVIRONMENT=Production
	networks:
	  - internal
	restart: unless-stopped

networks:
  internal:
	internal: true
	driver: bridge
```

---

## Incident Response

### 1. Security Incident Log

```csharp
public class SecurityIncident
{
	public DateTime Timestamp { get; set; }
	public string IncidentType { get; set; } // Invalid credentials, File validation failed, etc.
	public string Severity { get; set; } // Low, Medium, High, Critical
	public string SourceIP { get; set; }
	public string Details { get; set; }
}

// Log security incidents
_logger.LogWarning(
	"SECURITY: Invalid authentication attempt from {IP} at {Time}",
	context.Connection.RemoteIpAddress, DateTime.UtcNow);
```

### 2. Rate Limiting

Implement rate limiting to prevent abuse:

```csharp
public class RateLimitMiddleware
{
	private static readonly ConcurrentDictionary<string, (int Count, DateTime ResetTime)> _requests =
		new();

	public async Task InvokeAsync(HttpContext context)
	{
		var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
		var now = DateTime.UtcNow;

		if (_requests.TryGetValue(clientIp, out var rateLimitInfo))
		{
			if (now > rateLimitInfo.ResetTime)
			{
				_requests[clientIp] = (1, now.AddMinutes(1));
			}
			else if (rateLimitInfo.Count >= 100)
			{
				context.Response.StatusCode = 429; // Too Many Requests
				await context.Response.WriteAsync("Rate limit exceeded");
				return;
			}
			else
			{
				_requests[clientIp] = (rateLimitInfo.Count + 1, rateLimitInfo.ResetTime);
			}
		}
		else
		{
			_requests.TryAdd(clientIp, (1, now.AddMinutes(1)));
		}

		await _next(context);
	}
}
```

---

## Compliance

### GDPR Compliance

If handling personal data:

1. **Data Minimization**: Only collect necessary printer names and IPs
2. **Retention**: Implement automatic log deletion (e.g., 90 days)
3. **Audit Trail**: Maintain comprehensive logs of data access
4. **Privacy Policy**: Document data handling practices

```csharp
// Auto-delete old logs
public class LogCleanupService : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// Delete logs older than 90 days
				var cutoffDate = DateTime.UtcNow.AddDays(-90);
				await _logRepository.DeleteLogsBeforeAsync(cutoffDate);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during log cleanup");
			}

			await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
		}
	}
}
```

### SOC 2 Compliance

- ✅ Change management procedures
- ✅ Monitoring and alerting
- ✅ Incident response procedures
- ✅ Access controls
- ✅ Encryption standards
- ✅ Audit logs

---

## Security Checklist

### Pre-Deployment

- [ ] Enable HTTPS/TLS
- [ ] Implement authentication
- [ ] Configure firewall rules
- [ ] Set up reverse proxy
- [ ] Enable rate limiting
- [ ] Review error handling
- [ ] Enable audit logging
- [ ] Configure backup procedures
- [ ] Document security policies
- [ ] Conduct security review

### Post-Deployment

- [ ] Monitor access logs
- [ ] Check security alerts
- [ ] Review audit logs
- [ ] Verify firewall rules
- [ ] Test authentication
- [ ] Verify HTTPS working
- [ ] Check rate limiting
- [ ] Monitor resource usage
- [ ] Review recent patches
- [ ] Document incidents

---

## References

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Microsoft Security Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/security/)
- [NIST Cybersecurity Framework](https://www.nist.gov/cyberframework)
- [CWE Top 25](https://cwe.mitre.org/top25/)

---

**Last Updated**: 2024  
**Version**: 1.0  
**Review Frequency**: Quarterly
