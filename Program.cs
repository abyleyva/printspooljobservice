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

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5075);
});

var app = builder.Build();

//configuracion puerto personalizado
//app.Urls.Add("http://localhost:5075");

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
