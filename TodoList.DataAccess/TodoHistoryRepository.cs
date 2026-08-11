using Microsoft.EntityFrameworkCore;
using TodoList.Entities;

namespace TodoList.DataAccess
{
    public class TodoHistoryRepository : ITodoHistoryRepository
    {
        private readonly AppDbContext _context;

        public TodoHistoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddHistoryAsync(TodoHistory history)
        {
            await _context.TodoHistories.AddAsync(history);
            //await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<TodoHistory>> GetHistoryByTodoIdAsync(int todoId)
        {
            return await _context.TodoHistories
                         .AsNoTracking()
                         .Where(h => h.TodoId == todoId)
                         .OrderByDescending(h => h.CreatedDate)   
                         .ToListAsync();
            ;
        }
    }

    }