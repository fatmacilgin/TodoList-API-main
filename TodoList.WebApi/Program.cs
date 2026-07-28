using Microsoft.EntityFrameworkCore;
using TodoList.DataAccess;
using TodoList.Business;
using TodoList.WebApi.Endpoints;
using Hangfire;
using Hangfire.PostgreSql; // Hangfire PostgreSQL paketini kullandığınızdan emin olun
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// JWT Konfigürasyonu
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? "BuCokGizliVeUzunBirSecretKeyOlmaliRenderTarafinaEkle!";
var key = Encoding.UTF8.GetBytes(secretKey);

// 1. SERVİSLER
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// CORS (Her şeye izin ver)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Authentication & JWT
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

// PostgreSQL Veritabanı Bağlantısı
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        b => b.MigrationsAssembly("TodoList.DataAccess")
    ));

// Hangfire Servisleri (PostgreSQL için Yapılandırma)
//if (!string.IsNullOrEmpty(connectionString))
//{
//    builder.Services.AddHangfire(config => config
//        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
//        .UseSimpleAssemblyNameTypeSerializer()
//        .UseRecommendedSerializerSettings()
//        .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

//    builder.Services.AddHangfireServer();
//}

// Dependency Injection (Bağımlılıkların Enjeksiyonu)
builder.Services.AddScoped<ITodoRepository, TodoRepository>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<ITodoHistoryRepository, TodoHistoryRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

var app = builder.Build();

// 2. MIDDLEWARE SIRALAMASI
app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo API v1");
    c.RoutePrefix = string.Empty; // Swagger'ı ana dizinde (/) açar
});

// 🔴 VERİTABANI VE HANGFIRE MIGRATION
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Veritabanı tablolarını ve migration'ları otomatik oluşturur
        db.Database.Migrate();

        // Hangfire Job Tanımlama
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
    Console.WriteLine($"[KRİTİK HATA] DB/Hangfire Başlatılamadı: {ex.Message}");
}

// Güvenlik Middleware'leri
app.UseAuthentication();
app.UseAuthorization();

// API Endpoint Yönlendirmeleri
app.MapControllers();
app.MapAuthEndpoints();
app.MapTodoEndpoints();
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate(); // Tablolar yoksa otomatik oluşturur
}
app.Run();