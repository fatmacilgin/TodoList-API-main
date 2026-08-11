namespace TodoList.DataAccess
{
    public interface IUnitOfWork : IDisposable
    {
        ITodoRepository Todos { get; }
        ITodoHistoryRepository TodoHistories { get; }
        ISubTaskRepository SubTasks { get; } // 🚀 Yeni eklendi
        // Tüm değişiklikleri veritabanına tek seferde kaydeden metot
        Task<int> SaveChangesAsync();
        IUserRepository Users { get; }
    }
}