using TodoList.Entities;
using TodoList.Entities.DTOs;

namespace TodoList.Business;

public interface ITodoService
{
    Task<Todo> CreateTodoAsync(TodoCreateDto todoCreateDto);
    Task<List<Todo>> GetAllTodosAsync();
    Task<Todo?> GetTodoByIdAsync(int id);
    Task<bool> UpdateTodoAsync(int id, TodoUpdateDto todoUpdateDto);
    Task<bool> DeleteTodoAsync(int id);
    Task<IEnumerable<TodoHistory>> GetTodoHistoriesByTodoIdAsync(int todoId);
    Task Send10SecondReminderAsync(int todoId);
    Task CleanOldDeletedTodosAsync();

    // 🚀 Task Assignment (Görev Atama) Metotları
    Task<List<Todo>> GetAssignedTodosAsync(int userId);
    Task<bool> AssignTaskAsync(int todoId, int assignToUserId, int currentUserId);

    // SubTask Metotları
    Task<SubTask> AddSubTaskAsync(int todoId, SubTaskCreateDto subTaskDto);
    Task<bool> ToggleSubTaskAsync(int todoId, int subTaskId);
    Task<bool> DeleteSubTaskAsync(int todoId, int subTaskId);
}