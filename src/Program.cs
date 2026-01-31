using DataAnalyticsApi.Data;
using DataAnalyticsApi.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuration
var connectionString = builder.Configuration.GetValue<string>("ConnectionStrings:Default") ?? "Data Source=data.db";
if (connectionString.Contains("Host=") || connectionString.Contains("host="))
{
    // Assume Postgres connection string
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connectionString));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connectionString));
}

builder.Services.AddScoped<CsvLoader>();
builder.Services.AddHostedService<RefreshService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve a simple static UI from wwwroot (index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
