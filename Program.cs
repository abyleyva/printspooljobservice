var builder = WebApplication.CreateBuilder(args);
// Consider this related information: applicaction as a Windows Service
builder.Host.UseWindowsService();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.WebHost.ConfigureKestrel(options => 
{
    options.ListenAnyIP(5000);
    //options.ListenAnyIP(5226);
}
);

var app = builder.Build();

// Set the URLs for the application
app.Urls.Add("http://localhost:5000");

// Apply CORS policy
app.UseCors("AllowAllOrigins");


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
