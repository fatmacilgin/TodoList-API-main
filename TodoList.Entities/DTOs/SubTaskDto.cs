namespace TodoList.Entities.DTOs;

public class SubTaskDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int TaskId { get; set; }
    public class SubTaskCreateDto
    {
        public string Title { get; set; } = string.Empty;
    }

    public class SubTaskUpdateDto
    {
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
    }
}