using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TodoList.DataAccess;
using TodoList.Entities;
using TodoList.Entities.DTOs;

namespace TodoList.WebApi.Endpoints;

public static class TodoEndpoints
{
    public static void MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/todos").RequireAuthorization();

        // 1. GET: Tüm Todolar
        group.MapGet("/", async (ClaimsPrincipal userClaims, AppDbContext db) =>
        {
            var userId = GetUserId(userClaims);
            var todos = await db.Todos.Where(t => t.UserId == userId).ToListAsync();
            return Results.Ok(todos);
        });

        // 2. GET: ID'ye Göre Todo
        group.MapGet("/{id}", async (int id, ClaimsPrincipal userClaims, AppDbContext db) =>
        {
            var userId = GetUserId(userClaims);
            var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            return todo is not null ? Results.Ok(todo) : Results.NotFound("Görev bulunamadı.");
        });

        // 3. GET: Todo Geçmişi (History)
        group.MapGet("/{id}/history", async (int id, ClaimsPrincipal userClaims, AppDbContext db) =>
        {
            var userId = GetUserId(userClaims);
            var todoExists = await db.Todos.AnyAsync(t => t.Id == id && t.UserId == userId);

            if (!todoExists)
                return Results.NotFound("Görev bulunamadı veya yetkiniz yok.");

            var histories = await db.TodoHistories
                .Where(h => h.TodoId == id)
                .OrderByDescending(h => h.CreatedDate)
                .ToListAsync();

            return Results.Ok(histories);
        });

        // 4. POST: Yeni Todo Ekle + History
        group.MapPost("/", async (TodoCreateDto dto, ClaimsPrincipal userClaims, AppDbContext db) =>
        {
            var userId = GetUserId(userClaims);

            var todo = new Todo
            {
                Title = dto.Title,
                IsCompleted = 0, // Yeni eklenen görev varsayılan olarak 0 (tamamlanmadı)
                UserId = userId
            };

            db.Todos.Add(todo);
            await db.SaveChangesAsync();

            // 📜 HISTORY KAYDI
            var history = new TodoHistory
            {
                TodoId = todo.Id,
                Status = $"Görev oluşturuldu: '{todo.Title}'",
                CreatedDate = DateTime.Now
            };
            db.TodoHistories.Add(history);
            await db.SaveChangesAsync();

            return Results.Created($"/api/todos/{todo.Id}", todo);
        });

        // 5. PUT: Todo Güncelle + History
        group.MapPut("/{id}", async (int id, TodoUpdateDto dto, ClaimsPrincipal userClaims, AppDbContext db) =>
        {
            var userId = GetUserId(userClaims);
            var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (todo is null)
                return Results.NotFound("Güncellenecek görev bulunamadı.");

            string changeDescription = "";

            // Başlık Değişikliği
            if (todo.Title != dto.Title)
            {
                changeDescription = $"Başlık değiştirildi: '{todo.Title}' ➔ '{dto.Title}'";
            }

            // Durum Değişikliği (int karşılaştırması: 1 veya 0)
            int newIsCompleted = Convert.ToInt32(dto.IsCompleted); // DTO'dan bool gelse bile int'e çevrilir
            if (todo.IsCompleted != newIsCompleted)
            {
                var statusText = newIsCompleted == 1 ? "Tamamlandı" : "Tamamlanmadı (Geri Alındı)";
                changeDescription += string.IsNullOrEmpty(changeDescription)
                    ? $"Durum değiştirildi: {statusText}"
                    : $" ve Durum değiştirildi: {statusText}";
            }

            // Değerleri Güncelle
            todo.Title = dto.Title;
            todo.IsCompleted = newIsCompleted;

            // 📜 DEĞİŞİKLİK VARSA HISTORY EKLE
            if (!string.IsNullOrEmpty(changeDescription))
            {
                var history = new TodoHistory
                {
                    TodoId = todo.Id,
                    Status = changeDescription,
                    CreatedDate = DateTime.Now
                };
                db.TodoHistories.Add(history);
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // 6. DELETE: Todo Sil + History
        group.MapDelete("/{id}", async (int id, ClaimsPrincipal userClaims, AppDbContext db) =>
        {
            var userId = GetUserId(userClaims);
            var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (todo is null)
                return Results.NotFound("Silinecek görev bulunamadı.");

            var history = new TodoHistory
            {
                TodoId = todo.Id,
                Status = $"Görev silindi: '{todo.Title}'",
                CreatedDate = DateTime.Now
            };
            db.TodoHistories.Add(history);

            db.Todos.Remove(todo);
            await db.SaveChangesAsync();

            return Results.Ok("Görev başarıyla silindi.");
        });
    }

    private static int GetUserId(ClaimsPrincipal userClaims)
    {
        var claim = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var userId) ? userId : 0;
    }
}