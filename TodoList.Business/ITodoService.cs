using TodoList.Entities;
using TodoList.Entities.DTOs;

namespace TodoList.Business;

public interface ITodoService
{
    Task<List<Todo>> GetAllTodosAsync();
    Task<Todo?> GetTodoByIdAsync(int id);
    Task<Todo> CreateTodoAsync(TodoCreateDto todoCreateDto); // Ham Todo değil, DTO olmalı
    Task<bool> UpdateTodoAsync(int id, TodoUpdateDto todoUpdateDto); // Ham Todo değil, DTO olmalı
    Task<bool> DeleteTodoAsync(int id);
    Task<IEnumerable<TodoHistory>> GetTodoHistoriesByTodoIdAsync(int todoId);
    Task Send10SecondReminderAsync(int todoId);
    Task CleanOldDeletedTodosAsync();
}