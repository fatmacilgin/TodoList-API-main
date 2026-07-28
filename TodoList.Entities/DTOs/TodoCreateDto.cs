using System;
using System.Collections.Generic;
using System.Text;

namespace TodoList.Entities.DTOs;

public class TodoCreateDto
{
    public string Title { get; set; } = string.Empty;
}