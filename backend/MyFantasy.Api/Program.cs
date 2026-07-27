using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyFantasy.Api.Data;
using MyFantasy.Api.Fantasy;
using MyFantasy.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuración ----
builder.Services.Configure<FantasyOptions>(builder.Configuration.GetSection(FantasyOptions.SectionName));

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? new[] { "http://localhost:3000" };

// ---- Base de datos (MySQL vía Pomelo) ----
var connectionString = builder.Configuration.GetConnectionString("MySql")
    ?? "server=localhost;port=3306;database=myfantasy;user=root;password=";
// Versión fija para no requerir el servidor levantado al configurar. Ajusta en
// appsettings si usas MariaDB u otra versión de MySQL.
var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(connectionString, serverVersion));

// ---- Cliente de la API de LaLiga (token + datos) ----
builder.Services.AddHttpClient<IFantasyTokenManager, FantasyTokenManager>();
builder.Services.AddHttpClient<IFantasyApiClient, FantasyApiClient>((sp, client) =>
{
    var o = sp.GetRequiredService<IOptions<FantasyOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
});

// ---- Servicios de dominio ----
builder.Services.AddScoped<LeagueService>();
builder.Services.AddScoped<DeltaService>();
builder.Services.AddScoped<SyncService>();

// ---- Web ----
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// ---- Migración automática (configurable) ----
if (builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "No se pudo migrar la base de datos al arrancar. ¿Está MySQL levantado y la cadena de conexión es correcta?");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("frontend");
app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }));

app.Run();
