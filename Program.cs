using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Service like Windows Service (name the service for the SCM)
if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService(options =>
    {
        options.ServiceName = "PrintSpoolJobService";
    });

    // Logging Event Log (useful in Windows services) with SourceName
#pragma warning disable CA1416 // Already guarded by OperatingSystem.IsWindows()
    builder.Logging.AddEventLog(settings =>
    {
        settings.SourceName = "PrintSpoolJobService";
    });
#pragma warning restore CA1416
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("LaravelApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// URL/port configuration 
var configuredUrls = builder.Configuration["urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (!string.IsNullOrWhiteSpace(configuredUrls))
{
    // If full URLs are provided, use them as-is (e.g., launchSettings, ASPNETCORE_URLS)
    builder.WebHost.UseUrls(configuredUrls);
}
else
{
    // If no URLs, choose port according to precedence (--port, env, appsettings, default)
    var port = ResolvePort(builder.Configuration, args);
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Listen(IPAddress.Any, port);
    });
}

var app = builder.Build();

// Apply CORS policy
app.UseCors("LaravelApp");

// Swagger only in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.Run();

// -------- Helpers --------
static int ResolvePort(IConfiguration configuration, string[] args)
{
    // Valid TCP port range: 1–65535
    const int MinPort = 1;
    const int MaxPort = 65535;

    static bool IsValidPort(int port) => port >= MinPort && port <= MaxPort;

    // 1) Argument --port=#####
    foreach (var arg in args)
    {
        const string prefix = "--port=";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(arg.AsSpan(prefix.Length), out var p))
        {
            if (!IsValidPort(p))
                throw new ArgumentOutOfRangeException(nameof(args),
                    $"Invalid port '{p}' specified via --port. Must be between {MinPort} and {MaxPort}.");
            return p;
        }
    }

    // 2) Environment variables
    if (int.TryParse(Environment.GetEnvironmentVariable("SERVICE_PORT"), out var envPort))
    {
        if (!IsValidPort(envPort))
            throw new ArgumentOutOfRangeException("SERVICE_PORT",
                $"Invalid port '{envPort}' specified via SERVICE_PORT. Must be between {MinPort} and {MaxPort}.");
        return envPort;
    }
    if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var envPort2))
    {
        if (!IsValidPort(envPort2))
            throw new ArgumentOutOfRangeException("PORT",
                $"Invalid port '{envPort2}' specified via PORT. Must be between {MinPort} and {MaxPort}.");
        return envPort2;
    }

    // 3) appsettings (Http:Port)
    var cfgPortStr = configuration["Http:Port"];
    if (int.TryParse(cfgPortStr, out var cfgPort))
    {
        if (!IsValidPort(cfgPort))
            throw new ArgumentOutOfRangeException("Http:Port",
                $"Invalid port '{cfgPort}' specified in appsettings (Http:Port). Must be between {MinPort} and {MaxPort}.");
        return cfgPort;
    }

    // 4) Default
    return 5075;
}
