using TodoList.Entities;

namespace TodoList.DataAccess
{
    public interface ISubTaskRepository
    {
        Task<SubTask?> GetByIdAsync(int id);
        Task AddAsync(SubTask subTask);
        Task DeleteAsync(SubTask subTask);
    }
}