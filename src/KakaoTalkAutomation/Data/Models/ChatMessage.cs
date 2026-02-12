using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KakaoTalkAutomation.Data.Models;

/// <summary>
/// 카카오톡 채팅 메시지 엔티티
/// 수신/송신된 메시지를 데이터베이스에 저장하기 위한 모델입니다.
/// </summary>
[Table("ChatMessages")]
public class ChatMessage
{
    /// <summary>메시지 고유 ID (자동 증가)</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>채팅방 이름</summary>
    [Required]
    [MaxLength(200)]
    public string ChatRoomName { get; set; } = string.Empty;

    /// <summary>보낸 사람 이름</summary>
    [Required]
    [MaxLength(100)]
    public string Sender { get; set; } = string.Empty;

    /// <summary>메시지 내용</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>메시지 수신/발신 시각</summary>
    public DateTime MessageTime { get; set; } = DateTime.Now;

    /// <summary>DB 저장 시각</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>내가 보낸 메시지인지 여부 (true = 송신, false = 수신)</summary>
    public bool IsOutgoing { get; set; }

    /// <summary>추가 메모 또는 태그</summary>
    [MaxLength(500)]
    public string? Note { get; set; }

    public override string ToString()
    {
        var direction = IsOutgoing ? "→ 송신" : "← 수신";
        return $"[{MessageTime:yyyy-MM-dd HH:mm:ss}] [{direction}] [{ChatRoomName}] {Sender}: {Content}";
    }
}
