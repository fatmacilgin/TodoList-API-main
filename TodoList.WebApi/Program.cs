using Microsoft.EntityFrameworkCore;
using TodoList.DataAccess;
using TodoList.Business;
using TodoList.WebApi.Endpoints;
using Hangfire;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

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
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

// SQLite Veritabanı
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("TodoList.DataAccess")
    ));

// Hangfire Servisleri
//var hangfireDbPath = Path.Combine(AppContext.BaseDirectory, "hangfire.db");
//builder.Services.AddHangfire(config => config
//    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
//    .UseSimpleAssemblyNameTypeSerializer()
//    .UseRecommendedSerializerSettings()
//    .usepostg(hangfireDbPath));

builder.Services.AddHangfireServer();

// Dependency Injection (Bağımlılıkların Enjeksiyonu)
builder.Services.AddScoped<ITodoRepository, TodoRepository>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<ITodoHistoryRepository, TodoHistoryRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// 💡 NOT: Eğer Auth işlemlerini Servis/Repository katmanına taşıdıysan buraya eklemelisin:
// builder.Services.AddScoped<IUserRepository, UserRepository>();
// builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// 2. MIDDLEWARE SIRALAMASI
app.UseCors("AllowAll"); // Cross-Origin İstekleri için En Üste Alındı

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo API v1");
    c.RoutePrefix = string.Empty;
});

// 🔴 VERİTABANI VE HANGFIRE BAŞLATMA
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // EnsureCreated yerine Migration çalıştırıyoruz ki yeni eklediğimiz Users tablosu gelsin
        db.Database.Migrate();

        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        recurringJobManager.AddOrUpdate<ITodoService>(
            "nightly-database-cleanup-job",
            service => service.CleanOldDeletedTodosAsync(),
            Cron.Daily(3, 0)
        );
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[KRİTİK HATA] DB/Hangfire Başlatılamadı: {ex.Message}");
}

// Güvenlik Middleware'leri
app.UseAuthentication(); // 🔴 Kimlik Doğrulama
app.UseAuthorization();  // 🔴 Yetkilendirme

//app.UseHangfireDashboard("/hangfire");

// API Endpoint Yönlendirmeleri
app.MapControllers();
app.MapAuthEndpoints(); // 🔴 Yorum satırından çıkarıldı!
app.MapTodoEndpoints();

app.Run();