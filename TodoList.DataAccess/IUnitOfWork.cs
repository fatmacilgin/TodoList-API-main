namespace TodoList.DataAccess
{
    public interface IUnitOfWork : IDisposable
    {
        ITodoRepository Todos { get; }
        ITodoHistoryRepository TodoHistories { get; }

        // Tüm değişiklikleri veritabanına tek seferde kaydeden metot
        Task<int> SaveChangesAsync();
    }
}