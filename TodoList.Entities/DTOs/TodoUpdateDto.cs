using System;
using System.Collections.Generic;
using System.Text;
namespace TodoList.Entities.DTOs;

public class TodoUpdateDto
{
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } // Sadece int ve noktalı virgül olacak, Clone vs. varsa sil.
}