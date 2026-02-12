using KakaoTalkAutomation.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace KakaoTalkAutomation.Data;

/// <summary>
/// 애플리케이션 데이터베이스 컨텍스트
/// SQLite를 사용하여 카카오톡 메시지를 저장합니다.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>채팅 메시지 테이블</summary>
    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;

    private readonly string _dbPath;

    public AppDbContext()
    {
        // 기본 DB 파일 경로: 실행 파일과 같은 디렉터리
        _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kakaotalk_messages.db");
    }

    public AppDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kakaotalk_messages.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ChatMessage 테이블 설정
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            // 채팅방+시간 인덱스 (조회 성능 향상)
            entity.HasIndex(e => e.ChatRoomName)
                  .HasDatabaseName("IX_ChatMessages_ChatRoomName");

            entity.HasIndex(e => e.MessageTime)
                  .HasDatabaseName("IX_ChatMessages_MessageTime");

            entity.HasIndex(e => new { e.ChatRoomName, e.MessageTime })
                  .HasDatabaseName("IX_ChatMessages_ChatRoom_Time");

            // 보낸 사람 인덱스
            entity.HasIndex(e => e.Sender)
                  .HasDatabaseName("IX_ChatMessages_Sender");
        });
    }
}
