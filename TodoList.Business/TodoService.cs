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
            IsCompleted = 0
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

    public async Task<List<Todo>> GetAllTodosAsync()
    {
        return await _unitOfWork.Todos.GetAllAsync();
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
        string oldTitle = existingTodo.Title;
        int oldIsCompleted = existingTodo.IsCompleted;

        // 3. Yeni değerleri aktarıyoruz
        string newTitle = todoUpdateDto.Title;
        int newIsCompleted = todoUpdateDto.IsCompleted;

        existingTodo.Title = newTitle;
        existingTodo.IsCompleted = newIsCompleted;

        // 4. Detaylı History Mesajı Oluşturma
        string historyStatus = "";

        // A) Başlık Değiştiyse (Eski İsim -> Yeni İsim)
        if (oldTitle != newTitle && oldIsCompleted != newIsCompleted)
        {
            string stateText = newIsCompleted == 1 ? "Tamamlandı" : "Tamamlanmadı";
            historyStatus = $"Görev başlığı '{oldTitle}' -> '{newTitle}' olarak değiştirildi ve durumu '{stateText}' yapıldı.";
        }
        else if (oldTitle != newTitle)
        {
            historyStatus = $"Görev başlığı '{oldTitle}' iken '{newTitle}' olarak güncellendi.";
        }
        // B) Sadece Tamamlanma Durumu Değiştiyse
        else if (oldIsCompleted != newIsCompleted)
        {
            string stateText = newIsCompleted == 1 ? "Tamamlandı" : "Tamamlanmadı (Geri Alındı)";
            historyStatus = $"'{existingTodo.Title}' görevi '{stateText}' olarak işaretlendi.";
        }
        // C) Hiçbir Değişiklik Yapılmadıysa
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

        if (todo != null && todo.IsCompleted == 0)
        {
            Console.WriteLine($"[HATIRLATMA] Todo ID: {todo.Id} - '{todo.Title}' oluşturulalı tam 10 saniye oldu! Lütfen tamamlamayı unutmayın.");
        }
    }

    public async Task CleanOldDeletedTodosAsync()
{
    // 1. Soft-delete yapılmış (IsDeleted == true) görevleri çekiyoruz
    var deletedTodos = await _unitOfWork.Todos.GetAllDeletedAsync(); 
    // Not: Eğer repository'nizde direkt GetAllDeletedAsync yoksa, 
    // Tümünü çekip t.IsDeleted == true olanları filtreleyebilirsiniz.

    if (deletedTodos != null && deletedTodos.Any())
    {
        int count = deletedTodos.Count;

        foreach (var todo in deletedTodos)
        {
            // 2. Bu göreve bağlı History kayıtlarını ve görevin kendisini DB'den kalıcı siliyoruz (Hard Delete)
            _unitOfWork.Todos.DeleteAsync(todo);
        }

        await _unitOfWork.SaveChangesAsync();

        Console.WriteLine($"🧹 [GECE TEMİZLİĞİ] {DateTime.Now:dd.MM.yyyy HH:mm} - Soft-delete yapılmış toplam {count} adet eski görev veritabanından kalıcı olarak silindi.");
    }
    else
    {
        Console.WriteLine($"🧹 [GECE TEMİZLİĞİ] {DateTime.Now:dd.MM.yyyy HH:mm} - Silinecek eski görev bulunamadı. Veritabanı temiz!");
    }
}
}