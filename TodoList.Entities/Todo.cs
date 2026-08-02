using System;

namespace TodoList.Entities;

public class Todo
{
    // Mevcut alanlarınız (Bunlara dokunmuyoruz)
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }

    // YENİ ALANLAR (Güvenli Tanımlama):

    // 1. Yeni kolonlar için EF Core otomatik olarak varsayılan değer atar: 
    // bool için varsayılan değer false (yani 0) olacaktır. Mevcut verileriniz silinmez, otomatik false olur.
    public bool IsDeleted { get; set; } = false;

    // 2. Mevcut (eski) verilerinizin eklenme tarihini sistem geriye dönük bilemeyeceği için,
    // buraya varsayılan olarak şu anki zamanı bağlıyoruz. Eski veriler de bu tarihi alır.
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // 3. Güncelleme tarihi ilk başta boş olabileceği için sonuna "?" koyarak "Nullable" yapıyoruz.
    // Böylece SQLite eski verilere zorla bir tarih dayatmaz, null (boş) geçilmesine izin verir.
    public DateTime? LastModificationDate { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    // Mevcut Task entity'nizin içine ekleyin:
    public ICollection<SubTask> SubTasks { get; set; } = new List<SubTask>();
}
