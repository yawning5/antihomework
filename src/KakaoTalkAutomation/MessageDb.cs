using Microsoft.EntityFrameworkCore;

namespace KakaoTalkAutomation;

/// <summary>
/// 메시지를 SQLite에 저장하는 간단한 DB 클래스
///
/// Spring Boot로 비유하면:
///   AppDbContext ≈ EntityManager
///   ChatMessage  ≈ @Entity
///   SaveMessage  ≈ repository.save()
/// </summary>
public class MessageDb
{
    private readonly AppDb _db = new();

    /// <summary>DB 테이블을 자동 생성합니다.</summary>
    public void Initialize() => _db.Database.EnsureCreated();

    /// <summary>메시지를 DB에 저장합니다.</summary>
    public void Save(string chatRoom, string sender, string content, DateTime time, bool isOutgoing)
    {
        _db.Messages.Add(new ChatMessage
        {
            ChatRoomName = chatRoom,
            Sender = sender,
            Content = content,
            MessageTime = time,
            IsOutgoing = isOutgoing,
            CreatedAt = DateTime.Now
        });
        _db.SaveChanges();
    }

    /// <summary>최근 메시지를 조회합니다.</summary>
    public List<ChatMessage> GetRecent(int count = 20)
        => _db.Messages.OrderByDescending(m => m.CreatedAt).Take(count).ToList();
}

// ---- 엔티티 (JPA의 @Entity) ----

public class ChatMessage
{
    public int Id { get; set; }
    public string ChatRoomName { get; set; } = "";
    public string Sender { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime MessageTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsOutgoing { get; set; }
}

// ---- DbContext (JPA의 EntityManager) ----

public class AppDb : DbContext
{
    public DbSet<ChatMessage> Messages { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder opt)
        => opt.UseSqlite($"Data Source={Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "messages.db")}");
}
