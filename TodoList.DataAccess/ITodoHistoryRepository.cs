using TodoList.Entities;

namespace TodoList.DataAccess
{
    public interface ITodoHistoryRepository
    {
        Task AddHistoryAsync(TodoHistory history);
  
        Task<IEnumerable<TodoHistory>> GetHistoryByTodoIdAsync(int todoId);
    }
}