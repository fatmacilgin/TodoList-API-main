using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.EntityFrameworkCore;
using TodoList.Entities;

namespace TodoList.DataAccess;

public class TodoRepository : ITodoRepository
{
    private readonly AppDbContext _context;

    public TodoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Todo>> GetAllAsync()
    {
        // AsNoTracking() ekleyerek EF Core'un önbelleği baypas etmesini 
        // ve her seferinde direkt SQLite dosyasından en güncel veriyi okumasını sağlıyoruz.
        return await _context.Todos.AsNoTracking().ToListAsync();
    }

    public async Task<Todo?> GetByIdAsync(int id)
    {
        return await _context.Todos.FindAsync(id);
    }

    public async Task AddAsync(Todo todo)
    {
        await _context.Todos.AddAsync(todo);
        //await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Todo todo)
    {
        _context.Todos.Update(todo);
        //await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Todo todo)
    {
        _context.Todos.Remove(todo);
        //await _context.SaveChangesAsync();
    }
    // 1. int alana göre LINQ COUNT sorgusu
    public async Task<int> GetCompletedCountAsync()
    {
        return await _context.Todos
                             .Where(t => t.IsCompleted == true) // int olduğu için == 1 yapıyoruz
                             .CountAsync();
    }
    public async Task<List<Todo>> GetAllDeletedAsync()
    {
        return await _context.Todos
                             .IgnoreQueryFilters() // Global filtreyi geçici olarak devre dışı bırakır
                             .Where(t => t.IsDeleted == true)
                             .ToListAsync();
    }



}