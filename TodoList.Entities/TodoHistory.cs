using System;

namespace TodoList.Entities;

public class TodoHistory
{
    public int Id { get; set; }
    public int TodoId { get; set; }
    public string Status { get; set; } = string.Empty; // "Eklendi", "Güncellendi", "Silindi" gibi durumlar yazacağız
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public Todo Todo { get; set; }
}