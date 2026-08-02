namespace TodoList.DataAccess
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private ITodoRepository _todoRepository;
        private ITodoHistoryRepository _todoHistoryRepository;
        private ISubTaskRepository _subTaskRepository;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }
        public ISubTaskRepository SubTasks =>
            _subTaskRepository ??= new SubTaskRepository(_context);
        // Lazy-loading tarzı repository erişimi (isteğe bağlı direkt constructor'dan da alınabilir)
        public ITodoRepository Todos =>
            _todoRepository ??= new TodoRepository(_context);

        public ITodoHistoryRepository TodoHistories =>
            _todoHistoryRepository ??= new TodoHistoryRepository(_context);

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}