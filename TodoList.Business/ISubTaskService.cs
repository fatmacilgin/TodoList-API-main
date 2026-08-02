using TodoList.Entities.DTOs;

namespace TodoList.Business.Abstract;

public interface ISubTaskService
{
    Task<SubTaskDto> AddAsync(SubTaskCreateDto dto);
    Task<bool> ToggleAsync(int id);
    Task<bool> DeleteAsync(int id);
}