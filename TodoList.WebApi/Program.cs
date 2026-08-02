using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Text;
using TodoList.Business;
using TodoList.Business.Abstract;
using TodoList.Business.Concrete;
using TodoList.DataAccess;
using TodoList.WebApi.Endpoints;
using System.Text.Json.Serialization;

// ---------------------------------------------------------
// 0. AYARLAR & AYIKLAMA (Render & Npgsql Uyumluluğu)
// ---------------------------------------------------------
// PostgreSQL Npgsql sürücüsünün tarih kısıtlamasını gevşetir (UTC hatalarını engeller)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);   


// Render'dan gelen "postgres://" URI formatını Npgsql formatına dönüştürür
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
string formattedConnectionString = ConvertPostgresConnectionString(rawConnectionString);

// ---------------------------------------------------------
// 1. SERVİSLERİN EKLENMESİ (Dependency Injection)
// ---------------------------------------------------------

// Core & Controller Servisleri
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// 🚀 CORS Politikası Tanımlama
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "https://frontend-zo0w.onrender.com", // Render Frontend Adresiniz
                "http://localhost:5500",               // Local Testler İçin (VS Code Live Server vb.)
                "http://127.0.0.1:5500",
                "http://localhost:3000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? "BuCokGizliVeUzunBirSecretKeyOlmaliRenderTarafinaEkle!";
var key = Encoding.UTF8.GetBytes(secretKey);
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
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

// Hangfire Yapılandırması (RAM dostu 2 worker ile)
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

    // Worker sayısını varsayılan 20'den 2'ye düşürerek RAM kullanımını drastik şekilde azaltıyoruz
    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 2;
    });
}

// Uygulama Katmanı DI Tanımlamaları
builder.Services.AddScoped<ITodoRepository, TodoRepository>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<ITodoHistoryRepository, TodoHistoryRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ISubTaskService, SubTaskManager>();


var app = builder.Build();

// ---------------------------------------------------------
// 2. MIDDLEWARE ZİNCİRİ
// ---------------------------------------------------------

// CORS her zaman en üst sırada yer almalıdır
app.UseCors("AllowAll");

// Swagger UI Yapılandırması
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo API v1");
    c.RoutePrefix = string.Empty; // Swagger doğrudan ana dizinde (/) açılır
});

app.UseAuthentication();
app.UseAuthorization();
app.UseCors("AllowRenderFrontend");

// Minimal API ve Controller Route Mapping
app.MapControllers();
app.MapAuthEndpoints();
app.MapTodoEndpoints();

// ---------------------------------------------------------
// 3. ARKA PLAN İŞLEMLERİ (Kilitlenmeyi Önleyen Asenkron Başlatma)
// ---------------------------------------------------------
// Web sunucusu ayağa kalktıktan SONRA veritabanı migration'ını başlatır (Status 139 önleyici)
app.Lifetime.ApplicationStarted.Register(() =>
{
    Task.Run(() =>
    {
        try
        {
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                Console.WriteLine("[INFO] Database Migration başlatılıyor...");
                db.Database.Migrate();
                Console.WriteLine("[INFO] Database Migration tamamlandı.");

                var recurringJobManager = scope.ServiceProvider.GetService<IRecurringJobManager>();
                if (recurringJobManager != null)
                {
                    recurringJobManager.AddOrUpdate<ITodoService>(
                        "nightly-database-cleanup-job",
                        service => service.CleanOldDeletedTodosAsync(),
                        Cron.Daily(3, 0)
                    );
                    Console.WriteLine("[INFO] Hangfire Job tanımlandı.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[KRİTİK HATA] DB/Hangfire Başlatılamadı: {ex.Message}");
        }
    });
});
app.MapSubTaskEndpoints();
app.Run();

// ---------------------------------------------------------
// YARDIMCI METOTLAR
// ---------------------------------------------------------
static string ConvertPostgresConnectionString(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return connectionString;

    connectionString = connectionString.Trim();

    if (!connectionString.StartsWith("postgres://") && !connectionString.StartsWith("postgresql://"))
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