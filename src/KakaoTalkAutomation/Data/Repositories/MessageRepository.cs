using KakaoTalkAutomation.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KakaoTalkAutomation.Data.Repositories;

/// <summary>
/// 채팅 메시지 데이터 접근 계층
/// 메시지 저장, 조회, 검색 등 CRUD 기능을 제공합니다.
/// </summary>
public class MessageRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<MessageRepository> _logger;

    public MessageRepository(AppDbContext context, ILogger<MessageRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 데이터베이스를 초기화합니다 (테이블 자동 생성).
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        await _context.Database.EnsureCreatedAsync();
        _logger.LogInformation("데이터베이스 초기화 완료");
    }

    /// <summary>
    /// 메시지를 데이터베이스에 저장합니다.
    /// </summary>
    /// <param name="message">저장할 메시지 엔티티</param>
    /// <returns>저장된 메시지</returns>
    public async Task<ChatMessage> SaveMessageAsync(ChatMessage message)
    {
        message.CreatedAt = DateTime.Now;
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        _logger.LogDebug("메시지 저장 완료 - ID: {Id}, 채팅방: {ChatRoom}, 보낸사람: {Sender}",
            message.Id, message.ChatRoomName, message.Sender);

        return message;
    }

    /// <summary>
    /// 여러 메시지를 한 번에 저장합니다.
    /// </summary>
    /// <param name="messages">저장할 메시지 목록</param>
    /// <returns>저장된 메시지 수</returns>
    public async Task<int> SaveMessagesAsync(IEnumerable<ChatMessage> messages)
    {
        var messageList = messages.ToList();
        foreach (var msg in messageList)
        {
            msg.CreatedAt = DateTime.Now;
        }

        _context.ChatMessages.AddRange(messageList);
        var count = await _context.SaveChangesAsync();

        _logger.LogInformation("{Count}개 메시지 일괄 저장 완료", count);
        return count;
    }

    /// <summary>
    /// 특정 채팅방의 메시지를 조회합니다.
    /// </summary>
    /// <param name="chatRoomName">채팅방 이름</param>
    /// <param name="limit">최대 조회 수 (기본 50)</param>
    /// <returns>메시지 목록 (최신순)</returns>
    public async Task<List<ChatMessage>> GetMessagesByChatRoomAsync(string chatRoomName, int limit = 50)
    {
        return await _context.ChatMessages
            .Where(m => m.ChatRoomName == chatRoomName)
            .OrderByDescending(m => m.MessageTime)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// 최근 메시지를 조회합니다.
    /// </summary>
    /// <param name="limit">최대 조회 수 (기본 20)</param>
    /// <returns>메시지 목록 (최신순)</returns>
    public async Task<List<ChatMessage>> GetRecentMessagesAsync(int limit = 20)
    {
        return await _context.ChatMessages
            .OrderByDescending(m => m.MessageTime)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// 메시지 내용으로 검색합니다.
    /// </summary>
    /// <param name="keyword">검색 키워드</param>
    /// <param name="limit">최대 조회 수 (기본 50)</param>
    /// <returns>검색 결과 메시지 목록</returns>
    public async Task<List<ChatMessage>> SearchMessagesAsync(string keyword, int limit = 50)
    {
        return await _context.ChatMessages
            .Where(m => m.Content.Contains(keyword) || m.Sender.Contains(keyword))
            .OrderByDescending(m => m.MessageTime)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// 저장된 채팅방 이름 목록을 반환합니다.
    /// </summary>
    /// <returns>고유 채팅방 이름 목록</returns>
    public async Task<List<string>> GetChatRoomNamesAsync()
    {
        return await _context.ChatMessages
            .Select(m => m.ChatRoomName)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
    }

    /// <summary>
    /// 전체 메시지 수를 반환합니다.
    /// </summary>
    /// <returns>메시지 수</returns>
    public async Task<int> GetMessageCountAsync()
    {
        return await _context.ChatMessages.CountAsync();
    }

    /// <summary>
    /// 특정 기간의 메시지를 조회합니다.
    /// </summary>
    /// <param name="from">시작 날짜</param>
    /// <param name="to">종료 날짜</param>
    /// <returns>메시지 목록</returns>
    public async Task<List<ChatMessage>> GetMessagesByDateRangeAsync(DateTime from, DateTime to)
    {
        return await _context.ChatMessages
            .Where(m => m.MessageTime >= from && m.MessageTime <= to)
            .OrderByDescending(m => m.MessageTime)
            .ToListAsync();
    }
}
