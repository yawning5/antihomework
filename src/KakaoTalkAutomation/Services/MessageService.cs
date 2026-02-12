using KakaoTalkAutomation.Core;
using KakaoTalkAutomation.Data.Models;
using KakaoTalkAutomation.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace KakaoTalkAutomation.Services;

/// <summary>
/// 메시지 송수신 비즈니스 로직을 담당하는 서비스
/// Core 모듈과 Data 모듈을 연결합니다.
/// </summary>
public class MessageService
{
    private readonly MessageSender _sender;
    private readonly MessageReader _reader;
    private readonly KakaoTalkFinder _finder;
    private readonly MessageRepository _repository;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        MessageSender sender,
        MessageReader reader,
        KakaoTalkFinder finder,
        MessageRepository repository,
        ILogger<MessageService> logger)
    {
        _sender = sender;
        _reader = reader;
        _finder = finder;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// 특정 채팅방에 메시지를 보내고 DB에 기록합니다.
    /// </summary>
    /// <param name="chatRoomName">채팅방 이름</param>
    /// <param name="message">보낼 메시지</param>
    /// <returns>전송 성공 여부</returns>
    public async Task<bool> SendAndRecordMessageAsync(string chatRoomName, string message)
    {
        // 메시지 전송
        var success = _sender.SendMessage(chatRoomName, message);

        if (success)
        {
            // 전송 성공 시 DB에 기록
            var chatMessage = new ChatMessage
            {
                ChatRoomName = chatRoomName,
                Sender = "나",
                Content = message,
                MessageTime = DateTime.Now,
                IsOutgoing = true
            };

            await _repository.SaveMessageAsync(chatMessage);
            _logger.LogInformation("메시지 전송 및 DB 기록 완료 - 채팅방: {ChatRoom}", chatRoomName);
        }
        else
        {
            _logger.LogWarning("메시지 전송 실패 - 채팅방: {ChatRoom}", chatRoomName);
        }

        return success;
    }

    /// <summary>
    /// 채팅방의 새 수신 메시지를 읽어 DB에 저장합니다.
    /// </summary>
    /// <param name="chatRoomHandle">채팅방 창 핸들</param>
    /// <param name="chatRoomName">채팅방 이름</param>
    /// <returns>새로 저장된 메시지 수</returns>
    public async Task<int> ProcessNewMessagesAsync(IntPtr chatRoomHandle, string chatRoomName)
    {
        var newMessages = _reader.ReadNewMessages(chatRoomHandle);

        if (newMessages.Count == 0)
        {
            return 0;
        }

        var chatMessages = newMessages.Select(m => new ChatMessage
        {
            ChatRoomName = chatRoomName,
            Sender = m.Sender,
            Content = m.Content,
            MessageTime = m.Timestamp,
            IsOutgoing = false
        }).ToList();

        var savedCount = await _repository.SaveMessagesAsync(chatMessages);
        _logger.LogInformation("채팅방 '{ChatRoom}'에서 {Count}개 새 메시지 저장 완료",
            chatRoomName, savedCount);

        return savedCount;
    }

    /// <summary>
    /// 카카오톡 연결 상태를 확인합니다.
    /// </summary>
    /// <returns>(실행 중 여부, 메인 창 발견 여부, 채팅방 수)</returns>
    public (bool IsRunning, bool MainWindowFound, int ChatRoomCount) CheckConnection()
    {
        var isRunning = _finder.IsKakaoTalkRunning();
        var mainWindow = isRunning ? _finder.FindMainWindow() : IntPtr.Zero;
        var chatRooms = isRunning ? _finder.FindChatRooms() : new List<(IntPtr, string)>();

        return (isRunning, mainWindow != IntPtr.Zero, chatRooms.Count);
    }
}
