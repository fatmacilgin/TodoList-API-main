using Microsoft.EntityFrameworkCore;
using TodoList.Business.Abstract;
using TodoList.DataAccess;
using TodoList.Entities;
using TodoList.Entities.DTOs;

namespace TodoList.Business.Concrete;

public class SubTaskManager : ISubTaskService
{
    private readonly AppDbContext _context;

    public SubTaskManager(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SubTaskDto> AddAsync(SubTaskCreateDto dto)
    {
        var subTask = new SubTask
        {
            Title = dto.Title,
            TaskId = dto.TaskId,
            IsCompleted = false
        };

        _context.SubTasks.Add(subTask);
        await _context.SaveChangesAsync();

        return new SubTaskDto
        {
            Id = subTask.Id,
            Title = subTask.Title,
            IsCompleted = subTask.IsCompleted,
            TaskId = subTask.TaskId
        };
    }

    public async Task<bool> ToggleAsync(int id)
    {
        var subTask = await _context.SubTasks.FindAsync(id);
        if (subTask == null) return false;

        subTask.IsCompleted = !subTask.IsCompleted;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var subTask = await _context.SubTasks.FindAsync(id);
        if (subTask == null) return false;

        _context.SubTasks.Remove(subTask);
        await _context.SaveChangesAsync();
        return true;
    }
}