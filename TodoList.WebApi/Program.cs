using Microsoft.EntityFrameworkCore;
using TodoList.DataAccess;
using TodoList.Business;
using TodoList.WebApi.Endpoints;
using Hangfire;
using Hangfire.PostgreSql;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
// PostgreSQL Npgsql sürücüsünün tarih kısıtlamasını gevşetir
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------
// 0. CONNECTION STRING FORMAT DÖNÜŞTÜRÜCÜ (Render Uyumluluğu)
// ---------------------------------------------------------
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Render'dan gelen "postgres://" URI formatını Npgsql formatına dönüştürür
string formattedConnectionString = ConvertPostgresConnectionString(rawConnectionString);

// ---------------------------------------------------------
// 1. SERVİSLER
// ---------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? "BuCokGizliVeUzunBirSecretKeyOlmaliRenderTarafinaEkle!";
var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// CORS Yapılandırması
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "TodoListAPI",
            ValidAudience = jwtSettings["Audience"] ?? "TodoListUser",
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

// PostgreSQL EF Core Bağlantısı
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        formattedConnectionString,
        b => b.MigrationsAssembly("TodoList.DataAccess")
    ));

// Hangfire Yapılandırması
// Hangfire Server worker sayısını Free tier için düşürüyoruz (Varsayılan 20 çok fazla RAM harcar!)
// Hangfire Servisleri (Worker sayısını 2'ye düşürerek RAM kullanımını drastik azaltıyoruz)
if (!string.IsNullOrEmpty(formattedConnectionString))
{
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options =>
            options.UseNpgsqlConnection(formattedConnectionString),
            new PostgreSqlStorageOptions
            {
                PrepareSchemaIfNecessary = true
            }));

    // Free Tier RAM dostu worker ayarı:
    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 2;
    });
}

// Dependency Injection
builder.Services.AddScoped<ITodoRepository, TodoRepository>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<ITodoHistoryRepository, TodoHistoryRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

var app = builder.Build();

// ---------------------------------------------------------
// 2. MIDDLEWARE SIRALAMASI
// ---------------------------------------------------------

// CORS her zaman en üstte olmalı
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo API v1");
        c.RoutePrefix = string.Empty;
    });
}
else
{
    // Production'da RAM tasarrufu için SwaggerUI kapatabilir veya sadece JSON kalmasını sağlayabilirsiniz
    app.UseSwagger();
}

// Veritabanı Migration ve Background Job Başlatma
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        var recurringJobManager = scope.ServiceProvider.GetService<IRecurringJobManager>();
        if (recurringJobManager != null)
        {
            recurringJobManager.AddOrUpdate<ITodoService>(
                "nightly-database-cleanup-job",
                service => service.CleanOldDeletedTodosAsync(),
                Cron.Daily(3, 0)
            );
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[DB MIGRATION HAKKINDA]: {ex.Message}");
}

app.UseAuthentication();
app.UseAuthorization();

// Endpoint Mapping
app.MapControllers();
app.MapAuthEndpoints();
app.MapTodoEndpoints();

app.Run();

// ---------------------------------------------------------
// YARDIMCI METOT: Render URL Formatını Npgsql Formatına Çevirir
// ---------------------------------------------------------
static string ConvertPostgresConnectionString(string connectionString)
{
    if (string.IsNullOrEmpty(connectionString) || !connectionString.StartsWith("postgres://"))
    {
        return connectionString;
    }

    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':');

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Username = userInfo[0],
        Password = userInfo.Length > 1 ? userInfo[1] : "",
        Database = uri.AbsolutePath.TrimStart('/'),
        SslMode = SslMode.Require,
        TrustServerCertificate = true
    };

    return builder.ToString();
}