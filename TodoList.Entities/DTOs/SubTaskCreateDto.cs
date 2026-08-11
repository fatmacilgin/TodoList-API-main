namespace TodoList.Entities.DTOs; // veya projenizdeki DTO namespace'i

public class SubTaskCreateDto
{
    public string Title { get; set; } = string.Empty;
    public int TaskId { get; set; }
}