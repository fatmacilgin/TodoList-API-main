using TodoList.Entities;

namespace TodoList.DataAccess;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<List<User>> GetAllAsync();
}