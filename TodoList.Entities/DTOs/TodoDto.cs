using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TodoList.Entities.DTOs
{
    //TodoDto.cs(İstek yanıtlarında döneceğimiz veri)
    public class TodoDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string? CreatorName { get; set; }

        public int? AssignedUserId { get; set; }
        public string? AssignedUserName { get; set; }
    }
}
