using TodoList.Business.Abstract;
using TodoList.Entities.DTOs;

namespace TodoList.WebApi.Endpoints;

public static class SubTaskEndpoints
{
    public static void MapSubTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/subtasks")
                       .WithTags("SubTasks");

        // 1. Yeni Alt Görev Ekleme
        group.MapPost("/", async (SubTaskCreateDto dto, ISubTaskService subTaskService) =>
        {
            var result = await subTaskService.AddAsync(dto);
            return Results.Ok(result);
        });

        // 2. Alt Görevin Tamamlandı Durumunu Değiştirme (Toggle)
        group.MapPut("/{id:int}/toggle", async (int id, ISubTaskService subTaskService) =>
        {
            var result = await subTaskService.ToggleAsync(id);
            if (!result) return Results.NotFound();

            return Results.Ok();
        });

        // 3. Alt Görevi Silme
        group.MapDelete("/{id:int}", async (int id, ISubTaskService subTaskService) =>
        {
            var result = await subTaskService.DeleteAsync(id);
            if (!result) return Results.NotFound();

            return Results.Ok();
        });
    }
}