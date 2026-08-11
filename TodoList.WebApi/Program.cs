using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Text;
using System.Text.Json.Serialization;
using TodoList.Business;
using TodoList.Business.Abstract;
using TodoList.Business.Concrete;
using TodoList.DataAccess;
using TodoList.WebApi.Endpoints;

// ---------------------------------------------------------
// 0. AYARLAR & AYIKLAMA (Render & Npgsql Uyumluluğu)
// ---------------------------------------------------------
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
string formattedConnectionString = ConvertPostgresConnectionString(rawConnectionString);

// ---------------------------------------------------------
// 1. SERVİSLERİN EKLENMESİ (Dependency Injection)
// ---------------------------------------------------------
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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Todo API", Version = "v1" });

    // Swagger UI için JWT Bearer Desteği
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Token'ınızı girin. Örnek: Bearer eyJhbGciOi...",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? "BuCokGizliVeUzunBirSecretKeyOlmaliRenderTarafinaEkle!";
var key = Encoding.UTF8.GetBytes(secretKey);

// Minimal API ve Controller için JSON Yapılandırması (Circular Reference Önleme)
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
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
builder.Services.AddScoped<IUserRepository, UserRepository>();

var app = builder.Build();

// ---------------------------------------------------------
// 2. MIDDLEWARE ZİNCİRİ
// ---------------------------------------------------------
app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo API v1");
    c.RoutePrefix = string.Empty;
});

app.UseAuthentication();
app.UseAuthorization();

// Route & Endpoint Mapping
app.MapControllers();
app.MapAuthEndpoints();
app.MapTodoEndpoints();
app.MapSubTaskEndpoints();

// 🚀 TASK ASSIGNMENT İÇİN KULLANICI LİSTESİ ENDPOINT'İ
app.MapGet("/api/users", async (IUserRepository userRepo) =>
{
    var users = await userRepo.GetAllAsync();
    var result = users.Select(u => new { id = u.Id, name = u.FirstName });
    return Results.Ok(result);
}).RequireAuthorization();

// ---------------------------------------------------------
// 3. ARKA PLAN İŞLEMLERİ (Asenkron Başlatma)
// ---------------------------------------------------------
app.Lifetime.ApplicationStarted.Register(() =>
{
    Task.Run(() =>
    {
        try
        {
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                Console.WriteLine("[INFO] Database Migration denetleniyor...");
                db.Database.Migrate(); // Gerekirse canlı ortamda açabilirsiniz
                Console.WriteLine("[INFO] Database kontrolleri tamamlandı.");

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