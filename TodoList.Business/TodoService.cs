using TodoList.Business;
using TodoList.DataAccess;
using TodoList.Entities;
using TodoList.Entities.DTOs;

public class TodoService : ITodoService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITodoRepository _todoRepository;

    public TodoService(IUnitOfWork unitOfWork, ITodoRepository todoRepository)
    {
        _unitOfWork = unitOfWork;
        _todoRepository = todoRepository;
    }

    public async Task<Todo> CreateTodoAsync(TodoCreateDto todoCreateDto)
    {
        if (string.IsNullOrWhiteSpace(todoCreateDto.Title))
            throw new ArgumentException("Görev başlığı boş olamaz!");

        var todo = new Todo
        {
            Title = todoCreateDto.Title,
            IsCompleted = false
        };

        // 1. Todo'yu ekle
        await _unitOfWork.Todos.AddAsync(todo);

        // 2. İlk Oluşturulma History'si
        var history = new TodoHistory
        {
            Todo = todo,
            Status = $"'{todo.Title}' başlıklı yeni görev oluşturuldu.",
            CreatedDate = DateTime.Now
        };
        await _unitOfWork.TodoHistories.AddHistoryAsync(history);

        // 3. Tek Transaction Kayıt
        await _unitOfWork.SaveChangesAsync();

        return todo;
    }

    // 🚀 Tesis Edilen: Sistemdeki tüm görevleri getirir
    public async Task<List<Todo>> GetAllTodosAsync()
    {
        return await _unitOfWork.Todos.GetAllAsync();
    }

    // 🚀 Tesis Edilen: Yalnızca belirli bir kullanıcıya ATANMIŞ görevleri getirir
    public async Task<List<Todo>> GetAssignedTodosAsync(int userId)
    {
        var allTodos = await _unitOfWork.Todos.GetAllAsync();

        // Atanan kullanıcısı eşleşen ve silinmemiş görevleri filtreliyoruz
        return allTodos
            .Where(t => t.AssignedUserId == userId && !t.IsDeleted)
            .ToList();
    }

    // 🚀 Tesis Edilen: Görevi başka bir kullanıcıya atar
    public async Task<bool> AssignTaskAsync(int todoId, int assignToUserId, int currentUserId)
    {
        // 1. Görevi buluyoruz
        var todo = await _unitOfWork.Todos.GetByIdAsync(todoId);
        if (todo == null || todo.IsDeleted)
        {
            return false;
        }

        // 2. Atanacak kullanıcıyı buluyoruz (UnitOfWork üzerinde Users tanımınız yoksa Repo üzerinden de çekebilirsiniz)
        var assignedUser = await _unitOfWork.Users.GetByIdAsync(assignToUserId);
        if (assignedUser == null)
        {
            throw new KeyNotFoundException("Atanmak istenen kullanıcı bulunamadı.");
        }

        // 3. Görevin Atanan Kullanıcı ID'sini güncelliyoruz
        todo.AssignedUserId = assignToUserId;

        // 4. Tarihçe (History) kaydı oluşturuyoruz
        var history = new TodoHistory
        {
            Todo = todo,
            Status = $"Görev '{assignedUser.FirstName} {assignedUser.LastName}' kullanıcısına atandı.",
            CreatedDate = DateTime.Now
        };

        await _unitOfWork.TodoHistories.AddHistoryAsync(history);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<Todo?> GetTodoByIdAsync(int id)
    {
        return await _unitOfWork.Todos.GetByIdAsync(id);
    }

    public async Task<bool> UpdateTodoAsync(int id, TodoUpdateDto todoUpdateDto)
    {
        // 1. Veritabanındaki mevcut kaydı çekiyoruz
        var existingTodo = await _unitOfWork.Todos.GetByIdAsync(id);
        if (existingTodo == null)
        {
            return false;
        }

        // 2. Değişiklik öncesi ESKİ değerleri hafızada tutuyoruz
        string oldTitle = existingTodo.Title ?? string.Empty;
        bool oldIsCompleted = existingTodo.IsCompleted;

        // 3. Yeni değerleri alıyoruz
        string newTitle = todoUpdateDto.Title;
        bool newIsCompleted = todoUpdateDto.IsCompleted;

        existingTodo.Title = newTitle;
        existingTodo.IsCompleted = newIsCompleted;

        // 4. Detaylı History Mesajı Oluşturma
        string historyStatus = "";

        if (oldTitle != newTitle && oldIsCompleted != newIsCompleted)
        {
            string stateText = newIsCompleted ? "Tamamlandı" : "Tamamlanmadı";
            historyStatus = $"Görev başlığı '{oldTitle}' -> '{newTitle}' olarak değiştirildi ve durumu '{stateText}' yapıldı.";
        }
        else if (oldTitle != newTitle)
        {
            historyStatus = $"Görev başlığı '{oldTitle}' iken '{newTitle}' olarak güncellendi.";
        }
        else if (oldIsCompleted != newIsCompleted)
        {
            string stateText = newIsCompleted ? "Tamamlandı" : "Tamamlanmadı (Geri Alındı)";
            historyStatus = $"'{existingTodo.Title}' görevi '{stateText}' olarak işaretlendi.";
        }
        else
        {
            historyStatus = $"'{existingTodo.Title}' görevi güncellendi (değişiklik yapılmadı).";
        }

        // 5. History Kaydını Oluşturup Ekleme
        var history = new TodoHistory
        {
            Todo = existingTodo,
            Status = historyStatus,
            CreatedDate = DateTime.Now
        };

        await _unitOfWork.TodoHistories.AddHistoryAsync(history);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteTodoAsync(int id)
    {
        var existingTodo = await _unitOfWork.Todos.GetByIdAsync(id);
        if (existingTodo == null)
        {
            return false;
        }

        existingTodo.IsDeleted = true; // Soft delete

        // Silinme Tarihçe Kaydı
        var history = new TodoHistory
        {
            Todo = existingTodo,
            Status = $"'{existingTodo.Title}' başlıklı görev arayüzden silindi.",
            CreatedDate = DateTime.Now
        };

        await _unitOfWork.TodoHistories.AddHistoryAsync(history);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<TodoHistory>> GetTodoHistoriesByTodoIdAsync(int todoId)
    {
        return await _unitOfWork.TodoHistories.GetHistoryByTodoIdAsync(todoId);
    }

    public async Task Send10SecondReminderAsync(int todoId)
    {
        var todo = await _todoRepository.GetByIdAsync(todoId);

        if (todo != null && !todo.IsCompleted)
        {
            Console.WriteLine($"[HATIRLATMA] Todo ID: {todo.Id} - '{todo.Title}' oluşturulalı tam 10 saniye oldu! Lütfen tamamlamayı unutmayın.");
        }
    }

    public async Task CleanOldDeletedTodosAsync()
    {
        var deletedTodos = await _unitOfWork.Todos.GetAllDeletedAsync();

        if (deletedTodos != null && deletedTodos.Any())
        {
            int count = deletedTodos.Count;

            foreach (var todo in deletedTodos)
            {
                await _unitOfWork.Todos.DeleteAsync(todo);
            }

            await _unitOfWork.SaveChangesAsync();

            Console.WriteLine($"🧹 [GECE TEMİZLİĞİ] {DateTime.Now:dd.MM.yyyy HH:mm} - Soft-delete yapılmış toplam {count} adet eski görev veritabanından kalıcı olarak silindi.");
        }
        else
        {
            Console.WriteLine($"🧹 [GECE TEMİZLİĞİ] {DateTime.Now:dd.MM.yyyy HH:mm} - Silinecek eski görev bulunamadı. Veritabanı temiz!");
        }
    }

    // =======================================================
    // 🚀 SUBTASK İŞLEMLERİ (Alt Görev Metotları)
    // =======================================================

    public async Task<SubTask> AddSubTaskAsync(int todoId, SubTaskCreateDto subTaskDto)
    {
        var todo = await _unitOfWork.Todos.GetByIdAsync(todoId);
        if (todo == null)
            throw new KeyNotFoundException("Ana görev bulunamadı.");

        var subTask = new SubTask
        {
            Title = subTaskDto.Title,
            IsCompleted = false,
            TaskId = todoId
        };

        await _unitOfWork.SubTasks.AddAsync(subTask);

        // History Kaydı
        var history = new TodoHistory
        {
            Todo = todo,
            Status = $"Alt görev eklendi: '{subTask.Title}'",
            CreatedDate = DateTime.Now
        };
        await _unitOfWork.TodoHistories.AddHistoryAsync(history);

        await _unitOfWork.SaveChangesAsync();
        return subTask;
    }

    public async Task<bool> ToggleSubTaskAsync(int todoId, int subTaskId)
    {
        var subTask = await _unitOfWork.SubTasks.GetByIdAsync(subTaskId);
        if (subTask == null || subTask.TaskId != todoId)
            return false;

        subTask.IsCompleted = !subTask.IsCompleted;

        string stateText = subTask.IsCompleted ? "Tamamlandı" : "Tamamlanmadı";
        var history = new TodoHistory
        {
            TodoId = todoId,
            Status = $"Alt görev '{subTask.Title}' durumu güncellendi: {stateText}",
            CreatedDate = DateTime.Now
        };

        await _unitOfWork.TodoHistories.AddHistoryAsync(history);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteSubTaskAsync(int todoId, int subTaskId)
    {
        var subTask = await _unitOfWork.SubTasks.GetByIdAsync(subTaskId);
        if (subTask == null || subTask.TaskId != todoId)
            return false;

        var history = new TodoHistory
        {
            TodoId = todoId,
            Status = $"Alt görev silindi: '{subTask.Title}'",
            CreatedDate = DateTime.Now
        };

        await _unitOfWork.SubTasks.DeleteAsync(subTask);
        await _unitOfWork.TodoHistories.AddHistoryAsync(history);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }
}