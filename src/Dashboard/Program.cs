using Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL + EF (shared with API)
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration["DB_CONNECTION"]
        ?? builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

var port = builder.Configuration["DASHBOARD_PORT"] ?? "7001";
app.Run($"http://0.0.0.0:{port}");
