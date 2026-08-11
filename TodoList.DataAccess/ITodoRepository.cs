using System;
using System.Collections.Generic;
using System.Text;

using TodoList.Entities;

namespace TodoList.DataAccess;

public interface ITodoRepository
{
    Task<List<Todo>> GetAllAsync();
    Task<Todo?> GetByIdAsync(int id);
    Task AddAsync(Todo todo);
    Task UpdateAsync(Todo todo);
    Task DeleteAsync(Todo todo);
    // 1. Sadece tamamlanmış Todo'ların SAYISINI getiren LINQ metodu
    Task<int> GetCompletedCountAsync();
    Task<List<Todo>> GetAllDeletedAsync();
}
