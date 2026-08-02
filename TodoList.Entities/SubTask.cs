using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TodoList.Entities
{

    public class SubTask
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // İlişki (Foreign Key)
        public int TaskId { get; set; }
        public Todo? Task { get; set; } // Ana Entity adınız neyse (TodoTask, TaskItem vb.) değiştirebilirsiniz
    }
}
