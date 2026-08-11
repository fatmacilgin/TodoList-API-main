using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TodoList.Entities.DTOs
{
    // AssignTaskDto.cs (Bir görevi birine atamak için kullanılacak istek modeli)
    public class AssignTaskDto
    {
        public int TodoId { get; set; }
        public int AssignToUserId { get; set; }
    }
}
