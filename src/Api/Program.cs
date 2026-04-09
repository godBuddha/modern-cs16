using System.Text;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL + EF Core
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration["DB_CONNECTION"]
        ?? builder.Configuration.GetConnectionString("DefaultConnection")));

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opts =>
{
    opts.Password.RequiredLength = 6;
    opts.Password.RequireNonAlphanumeric = false;
    opts.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT Auth
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? "change_this_to_a_very_long_random_secret_key";
builder.Services.AddAuthentication(opts =>
{
    opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opts =>
{
    opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddCors(opts => opts.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// HttpClient for RCON proxy
builder.Services.AddHttpClient();
builder.Services.AddSingleton<Api.Services.DockerGameServerService>();

var app = builder.Build();

// Auto-create schema if not exists (no migrations files needed)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // EnsureCreated creates all tables based on EF model without needing migration files
    db.Database.EnsureCreated();

    // Manual migration: tạo bảng GameServers nếu chưa tồn tại
    // (EnsureCreated không thêm table mới vào DB đã có)
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""GameServers"" (
            ""Id""           SERIAL PRIMARY KEY,
            ""Name""         TEXT NOT NULL DEFAULT '',
            ""Host""         TEXT NOT NULL DEFAULT '',
            ""Port""         INTEGER NOT NULL DEFAULT 27015,
            ""RconPassword"" TEXT NOT NULL DEFAULT '',
            ""CurrentMap""   TEXT NOT NULL DEFAULT 'de_dust2',
            ""Description""  TEXT NOT NULL DEFAULT '',
            ""IsActive""     BOOLEAN NOT NULL DEFAULT TRUE,
            ""MaxPlayers""   INTEGER NOT NULL DEFAULT 32,
            ""CreatedAt""    TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
        );

        INSERT INTO ""GameServers"" (""Id"",""Name"",""Host"",""Port"",""CurrentMap"",""Description"",""MaxPlayers"",""RconPassword"",""IsActive"",""CreatedAt"")
        VALUES
            (1, '🇮🇹 Italy #1', '192.168.1.2', 27015, 'cs_italy',   'Server Italy vui vẻ #1',    32, '', TRUE, '2026-01-01 00:00:00+00'),
            (2, '🇮🇹 Italy #2', '192.168.1.2', 27016, 'cs_italy',   'Server Italy vui vẻ #2',    32, '', TRUE, '2026-01-01 00:00:00+00'),
            (3, '🇮🇹 Italy #3', '192.168.1.2', 27017, 'cs_italy',   'Server Italy vui vẻ #3',    32, '', TRUE, '2026-01-01 00:00:00+00'),
            (4, '💣 Dust2',     '192.168.1.2', 27018, 'de_dust2',   'Classic Dust2 deathmatch', 32, '', TRUE, '2026-01-01 00:00:00+00'),
            (5, '🔥 Inferno',   '192.168.1.2', 27019, 'de_inferno', 'Server Inferno cạnh tranh',32, '', TRUE, '2026-01-01 00:00:00+00'),
            (6, '☢️ Nuke',      '192.168.1.2', 27020, 'de_nuke',    'Server Nuke competitive',  32, '', TRUE, '2026-01-01 00:00:00+00')
        ON CONFLICT (""Id"") DO NOTHING;
    ");
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Static files for uploads
app.UseStaticFiles();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));

app.MapControllers();

var port = builder.Configuration["API_PORT"] ?? "7000";
app.Run($"http://0.0.0.0:{port}");
