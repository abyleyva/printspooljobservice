using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Servicio como Windows Service (nombra el servicio para el SCM)
builder.Host.UseWindowsService(options =>
{ 
    options.ServiceName = "PrintSpoolJobService";
});

// Logging al Event Log (útil en servicios Windows) con SourceName
builder.Logging.AddEventLog(settings =>
{
    settings.SourceName = "PrintSpoolJobService";
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("LaravelApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Controladores y Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuración de URL/puerto
var configuredUrls = builder.Configuration["urls"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (!string.IsNullOrWhiteSpace(configuredUrls))
{
    // Si se proporcionan URLs completas, úsalas tal cual (ej. launchSettings, ASPNETCORE_URLS)
    builder.WebHost.UseUrls(configuredUrls);
}
else
{
    // Si no hay URLs, elige puerto según precedencia (--port, env, appsettings, default)
    var port = ResolvePort(builder.Configuration, args);
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Listen(IPAddress.Any, port);
    });
}

var app = builder.Build();

// Aplicar CORS policy
app.UseCors("LaravelApp");

// Swagger sólo en desarrollo
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
    // 1) Argumento --port=#####
    foreach (var arg in args)
    {
        const string prefix = "--port=";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(arg.AsSpan(prefix.Length), out var p) && p > 0)
            return p;
    }

    // 2) Variables de entorno
    if (int.TryParse(Environment.GetEnvironmentVariable("SERVICE_PORT"), out var envPort) && envPort > 0)
        return envPort;
    if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var envPort2) && envPort2 > 0)
        return envPort2;

    // 3) appsettings (Http:Port)
    var cfgPortStr = configuration["Http:Port"];
    if (int.TryParse(cfgPortStr, out var cfgPort) && cfgPort > 0)
        return cfgPort;

    // 4) Por defecto
    return 5075;
}
