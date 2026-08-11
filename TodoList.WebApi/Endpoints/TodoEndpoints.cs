using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TodoList.Business;
using TodoList.DataAccess;
using TodoList.Entities;
using TodoList.Entities.DTOs;

namespace TodoList.WebApi.Endpoints;

public static class TodoEndpoints
{
    public static void MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/todos").RequireAuthorization();

        // 🚀 1. Tüm görevleri getir (Diğer kullanıcıların görevleri dahil)
        group.MapGet("/all", async (ITodoService todoService) =>
        {
            var todos = await todoService.GetAllTodosAsync();
            return Results.Ok(todos);
        });

        // 🚀 2. Giriş yapan kullanıcıya atanan görevleri getir
        group.MapGet("/my-assigned-tasks", async (ClaimsPrincipal userClaims, ITodoService todoService) =>
        {
            int currentUserId = GetUserId(userClaims);
            var assignedTodos = await todoService.GetAssignedTodosAsync(currentUserId);
            return Results.Ok(assignedTodos);
        });

        // 🚀 3. Başka bir kullanıcıya görev ata
        group.MapPost("/assign", async (AssignTaskDto dto, ClaimsPrincipal userClaims, ITodoService todoService) =>
        {
            int currentUserId = GetUserId(userClaims);
            var result = await todoService.AssignTaskAsync(dto.TodoId, dto.AssignToUserId, currentUserId);

            if (!result)
                return Results.BadRequest("Görev atanamadı. Görev bulunamadı veya geçersiz.");

            return Results.Ok(new { message = "Görev başarıyla atandı." });
        });

        // 1. GET: Tüm Todolar (SubTasks ile birlikte)
        group.MapGet("/", async (ClaimsPrincipal userClaims, AppDbContext db) =>
        {
            var userId = GetUserId(userClaims);
            var todos = await db.Todos
                .Include(t => t.SubTasks)
                .Where(t => t.UserId == userId && !t.IsDeleted)
                .ToListAsync();

            return Results.Ok(todos);
        });

        // 2. GET: ID'ye Göre Todo
        group.MapGet("/{id}", async (int id, ClaimsPrincipal userClaims, AppDbContext db) =>
        {
            var userId = GetUserId(userClaims);
            var todo = await db.Todos
                .Include(t => t.SubTasks)
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && !t.IsDeleted);

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
                IsCompleted = false,
                UserId = userId
            };

            db.Todos.Add(todo);
            await db.SaveChangesAsync();

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
            var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && !t.IsDeleted);

            if (todo is null)
                return Results.NotFound("Güncellenecek görev bulunamadı.");

            string changeDescription = "";

            if (todo.Title != dto.Title)
            {
                changeDescription = $"Başlık değiştirildi: '{todo.Title}' ➔ '{dto.Title}'";
            }

            if (todo.IsCompleted != dto.IsCompleted)
            {
                var statusText = dto.IsCompleted ? "Tamamlandı" : "Tamamlanmadı (Geri Alındı)";
                changeDescription += string.IsNullOrEmpty(changeDescription)
                    ? $"Durum değiştirildi: {statusText}"
                    : $" ve Durum değiştirildi: {statusText}";
            }

            todo.Title = dto.Title;
            todo.IsCompleted = dto.IsCompleted;

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

            var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (todo is null)
                return Results.NotFound("Silinecek görev bulunamadı (zaten silinmiş veya mevcut değil).");

            todo.IsDeleted = true; // Soft Delete

            var history = new TodoHistory
            {
                TodoId = todo.Id,
                Status = $"Görev silindi: '{todo.Title}'",
                CreatedDate = DateTime.Now
            };
            db.TodoHistories.Add(history);

            await db.SaveChangesAsync();

            return Results.Ok("Görev başarıyla silindi.");
        });

        // =======================================================
        // 🚀 SUBTASK ENDPOINT'LERİ
        // =======================================================

        // 7. POST: Todo'ya Alt Görev Ekle + History
        // 7. POST: Todo'ya Alt Görev Ekle + History
        group.MapPost("/{todoId}/subtasks", async (int todoId, SubTaskCreateDto dto, ClaimsPrincipal userClaims, AppDbContext db) =>
        {
            var userId = GetUserId(userClaims);

            // 🚀 DÜZELTME: Kullanıcı görevin oluşturanı (UserId) VEYA atananı (AssignedUserId) ise alt görev ekleyebilsin
            var todo = await db.Todos.FirstOrDefaultAsync(t =>
                t.Id == todoId &&
                (t.UserId == userId || t.AssignedUserId == userId) &&
                !t.IsDeleted);

            if (todo is null)
                return Results.NotFound("Ana görev bulunamadı veya yetkiniz yok.");

            var subTask = new SubTask
            {
                Title = dto.Title,
                IsCompleted = false,
                TaskId = todoId
            };

            db.SubTasks.Add(subTask);

            var history = new TodoHistory
            {
                TodoId = todoId,
                Status = $"Alt görev eklendi: '{subTask.Title}'",
                CreatedDate = DateTime.Now
            };
            db.TodoHistories.Add(history);

            await db.SaveChangesAsync();
            return Results.Created($"/api/todos/{todoId}/subtasks/{subTask.Id}", subTask);
        });

        // 8. PUT: Alt Görev Güncelle + History
        group.MapPut("/{todoId}/subtasks/{subTaskId}", async (int todoId, int subTaskId, SubTaskDto dto, ClaimsPrincipal userClaims, AppDbContext db) =>
        {
            var userId = GetUserId(userClaims);

            var todoExists = await db.Todos.AnyAsync(t => t.Id == todoId && t.UserId == userId && !t.IsDeleted);
            if (!todoExists)
                return Results.NotFound("Ana görev bulunamadı veya yetkiniz yok.");

            var subTask = await db.SubTasks.FirstOrDefaultAsync(st => st.Id == subTaskId && st.TaskId == todoId);
            if (subTask is null)
                return Results.NotFound("Alt görev bulunamadı.");

            string statusText = dto.IsCompleted ? "Tamamlandı" : "Tamamlanmadı yapıldı";

            var history = new TodoHistory
            {
                TodoId = todoId,
                Status = $"Alt görev '{subTask.Title}' durumu güncellendi: {statusText}",
                CreatedDate = DateTime.Now
            };

            subTask.Title = dto.Title;
            subTask.IsCompleted = dto.IsCompleted;

            db.TodoHistories.Add(history);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        // 9. DELETE: Alt Görev Sil + History
        group.MapDelete("/{todoId}/subtasks/{subTaskId}", async (int todoId, int subTaskId, ClaimsPrincipal userClaims, AppDbContext db) =>
        {
            var userId = GetUserId(userClaims);

            var todoExists = await db.Todos.AnyAsync(t => t.Id == todoId && t.UserId == userId && !t.IsDeleted);
            if (!todoExists)
                return Results.NotFound("Ana görev bulunamadı veya yetkiniz yok.");

            var subTask = await db.SubTasks.FirstOrDefaultAsync(st => st.Id == subTaskId && st.TaskId == todoId);
            if (subTask is null)
                return Results.NotFound("Silinecek alt görev bulunamadı.");

            var history = new TodoHistory
            {
                TodoId = todoId,
                Status = $"Alt görev silindi: '{subTask.Title}'",
                CreatedDate = DateTime.Now
            };
            db.TodoHistories.Add(history);

            db.SubTasks.Remove(subTask);
            await db.SaveChangesAsync();

            return Results.Ok("Alt görev başarıyla silindi.");
        });
    }

    private static int GetUserId(ClaimsPrincipal userClaims)
    {
        var claim = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var userId) ? userId : 0;
    }
}