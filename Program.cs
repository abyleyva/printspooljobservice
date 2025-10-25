using Microsoft.AspNetCore.HttpOverrides;

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

// HTTP client para descarga de logos remotos
builder.Services.AddHttpClient();

var isDev = builder.Environment.IsDevelopment();

// CORS: leer orígenes desde configuración
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            if (isDev)
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .SetPreflightMaxAge(TimeSpan.FromHours(1));
            }
            else
            {
                // En Producción, sin orígenes configurados -> no habilitar CORS (se aplicará no usar UseCors)
            }
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .SetPreflightMaxAge(TimeSpan.FromHours(1));
        }
    });
});

// Controladores y Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Mover configuración de Kestrel a appsettings/variables de entorno
builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.Configure(context.Configuration.GetSection("Kestrel"));
});

// Health checks
builder.Services.AddHealthChecks();

// Apagado limpio del servicio
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Seguridad y redirecciones solo en Producción
if (!app.Environment.IsDevelopment())
{
    // Soporte para ejecución detrás de proxy/reverse proxy (X-Forwarded-For / X-Forwarded-Proto)
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    app.UseExceptionHandler();
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Aplicar CORS solo si está habilitado (dev) o hay orígenes configurados
if (isDev || allowedOrigins.Length > 0)
{
    app.UseCors("DefaultCors");
}

// Swagger sólo en desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
