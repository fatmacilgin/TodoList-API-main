using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.EntityFrameworkCore;
using TodoList.Entities;

namespace TodoList.DataAccess;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Todo> Todos => Set<Todo>(); // yeni yöntem: null gibi compiler errorlarlakarışılaşılmaz.

    public DbSet<User> Users { get; set; }
    public DbSet<TodoHistory> TodoHistories { get; set; }// eski yöntem

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. User ve Todo İlişkisi (Bire-Çok)
        modelBuilder.Entity<Todo>()
            .HasOne(t => t.User)
            .WithMany(u => u.Todos)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 2. Todo ve TodoHistory İlişkisi (Çakışmayı önlemek için FK'yı açıkça belirtiyoruz)
        modelBuilder.Entity<TodoHistory>()
            .HasOne(h => h.Todo)            // Varsa navigation property adın (ör: h.Todo)
            .WithMany()                      // Veya .WithMany(t => t.Histories)
            .HasForeignKey(h => h.TodoId)    // Foreign Key alanını doğrudan bağlıyoruz
            .OnDelete(DeleteBehavior.Cascade);
    }
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Eklenen yeni Todo'ları yakala
        var addedTodos = ChangeTracker.Entries<Todo>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        // Tek SQL transaction ile hem Todo hem de History kaydedilir!
        return await base.SaveChangesAsync(cancellationToken);
    }

}
