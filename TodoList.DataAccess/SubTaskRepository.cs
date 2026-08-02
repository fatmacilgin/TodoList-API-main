using Microsoft.EntityFrameworkCore;
using TodoList.Entities;

namespace TodoList.DataAccess
{
    public class SubTaskRepository : ISubTaskRepository
    {
        private readonly AppDbContext _context;

        public SubTaskRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SubTask?> GetByIdAsync(int id)
        {
            return await _context.SubTasks.FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task AddAsync(SubTask subTask)
        {
            await _context.SubTasks.AddAsync(subTask);
        }

        public async Task DeleteAsync(SubTask subTask)
        {
            _context.SubTasks.Remove(subTask);
            await Task.CompletedTask;
        }
    }
}