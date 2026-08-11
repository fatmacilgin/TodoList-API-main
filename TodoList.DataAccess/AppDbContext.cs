using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TodoList.Entities;

namespace TodoList.DataAccess;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TodoHistory> TodoHistories => Set<TodoHistory>();
    public DbSet<SubTask> SubTasks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. User ve Todo İlişkisi (Bire-Çok)
        modelBuilder.Entity<Todo>()
            .HasOne(t => t.User)
            .WithMany(u => u.Todos)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 2. 🆕 Atanan Kullanıcı (AssignedUser - Todo İlişkisi)
        modelBuilder.Entity<Todo>()
            .HasOne(t => t.AssignedUser)
            .WithMany(u => u.AssignedTodos)
            .HasForeignKey(t => t.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull); // Kullanıcı silinirse görev silinmesin, atama boşa çıksın.

        // 2. Todo ve TodoHistory İlişkisi
        modelBuilder.Entity<TodoHistory>()
            .HasOne(h => h.Todo)
            .WithMany()
            .HasForeignKey(h => h.TodoId)
            .OnDelete(DeleteBehavior.Cascade);

        // SubTask - Task İlişkisi
        modelBuilder.Entity<SubTask>()
            .HasOne(s => s.Task)
            .WithMany(t => t.SubTasks)
            .HasForeignKey(s => s.TaskId)
            .OnDelete(DeleteBehavior.Cascade); // Ana görev silinirse alt görevler de otomatik silinsin

        
    }
    

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var addedTodos = ChangeTracker.Entries<Todo>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        foreach (var todo in addedTodos)
        {
            TodoHistories.Add(new TodoHistory
            {
                Todo = todo,
                Status = "Görev oluşturuldu",
                CreatedDate = DateTime.UtcNow
            });
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}