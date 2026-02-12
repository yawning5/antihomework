using KakaoTalkAutomation.Core;
using KakaoTalkAutomation.Data;
using KakaoTalkAutomation.Data.Models;
using KakaoTalkAutomation.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KakaoTalkAutomation.Services;

/// <summary>
/// 카카오톡 메시지 모니터링 백그라운드 서비스
/// 설정된 채팅방을 주기적으로 폴링하여 새 메시지를 감지하고 DB에 저장합니다.
///
/// ※ MessageRepository는 Scoped 서비스이므로, IServiceScopeFactory를 통해
///    매 폴링마다 새 scope를 만들어 사용합니다.
/// </summary>
public class MessageMonitorService : BackgroundService
{
    private readonly ILogger<MessageMonitorService> _logger;
    private readonly KakaoTalkFinder _finder;
    private readonly MessageReader _reader;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>폴링 간격 (밀리초)</summary>
    private int _pollingIntervalMs = 2000;

    /// <summary>모니터링 대상 채팅방 이름 목록</summary>
    private readonly List<string> _monitoredChatRooms = new();

    /// <summary>모니터링 활성화 여부</summary>
    private bool _isMonitoring;

    /// <summary>모니터링 시작 후 첫 폴링 여부 (기존 메시지 초기화용)</summary>
    private bool _firstPoll = true;

    /// <summary>새 메시지 수신 시 발생하는 이벤트</summary>
    public event Action<ChatMessage>? OnNewMessageReceived;

    public MessageMonitorService(
        ILogger<MessageMonitorService> logger,
        KakaoTalkFinder finder,
        MessageReader reader,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _finder = finder;
        _reader = reader;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// 모니터링을 시작합니다.
    /// </summary>
    /// <param name="chatRoomNames">모니터링할 채팅방 이름 목록 (빈 목록이면 모든 열린 채팅방)</param>
    public void StartMonitoring(IEnumerable<string>? chatRoomNames = null)
    {
        _monitoredChatRooms.Clear();
        if (chatRoomNames != null)
        {
            _monitoredChatRooms.AddRange(chatRoomNames);
        }

        _firstPoll = true;
        _isMonitoring = true;
        _logger.LogInformation("메시지 모니터링 시작 - 대상 채팅방: {Rooms}",
            _monitoredChatRooms.Count > 0
                ? string.Join(", ", _monitoredChatRooms)
                : "모든 열린 채팅방");
    }

    /// <summary>
    /// 모니터링을 중지합니다.
    /// </summary>
    public void StopMonitoring()
    {
        _isMonitoring = false;
        _logger.LogInformation("메시지 모니터링 중지");
    }

    /// <summary>
    /// 모니터링 활성화 상태를 반환합니다.
    /// </summary>
    public bool IsMonitoring => _isMonitoring;

    /// <summary>
    /// 폴링 간격을 설정합니다.
    /// </summary>
    /// <param name="intervalMs">폴링 간격 (밀리초, 최소 500ms)</param>
    public void SetPollingInterval(int intervalMs)
    {
        _pollingIntervalMs = Math.Max(500, intervalMs);
        _logger.LogInformation("폴링 간격 변경: {Interval}ms", _pollingIntervalMs);
    }

    /// <summary>
    /// 백그라운드 서비스 실행 로직
    /// 주기적으로 카카오톡 채팅방을 확인하고 새 메시지를 감지합니다.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("메시지 모니터 서비스가 시작되었습니다.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_isMonitoring)
                {
                    await PollForNewMessages();
                }

                await Task.Delay(_pollingIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "모니터링 루프 중 오류 발생");
                await Task.Delay(5000, stoppingToken); // 오류 시 5초 대기
            }
        }

        _logger.LogInformation("메시지 모니터 서비스가 종료되었습니다.");
    }

    /// <summary>
    /// 새 메시지를 폴링합니다.
    /// </summary>
    private async Task PollForNewMessages()
    {
        // 카카오톡 실행 확인
        if (!_finder.IsKakaoTalkRunning())
        {
            return;
        }

        // 열린 채팅방 목록 가져오기
        var chatRooms = _finder.FindChatRooms();

        foreach (var (handle, name) in chatRooms)
        {
            // 모니터링 대상 필터링
            if (_monitoredChatRooms.Count > 0 &&
                !_monitoredChatRooms.Any(m =>
                    name.Contains(m, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                // 첫 폴링 시 기존 메시지 초기화 (중복 방지)
                if (_firstPoll)
                {
                    _reader.InitializeMessageCount(handle);
                    _logger.LogDebug("채팅방 '{ChatRoom}' 기존 메시지 초기화 완료", name);
                    continue;
                }

                // 새 메시지 읽기
                var newMessages = _reader.ReadNewMessages(handle);

                foreach (var msg in newMessages)
                {
                    var chatMessage = new ChatMessage
                    {
                        ChatRoomName = name,
                        Sender = msg.Sender,
                        Content = msg.Content,
                        MessageTime = msg.Timestamp,
                        IsOutgoing = false
                    };

                    // Scoped 서비스(Repository)를 사용하기 위해 새 scope 생성
                    using var scope = _scopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<MessageRepository>();
                    await repository.SaveMessageAsync(chatMessage);

                    // 이벤트 발생
                    OnNewMessageReceived?.Invoke(chatMessage);

                    _logger.LogInformation(
                        "새 메시지 수신 - 채팅방: {ChatRoom}, 보낸사람: {Sender}, 내용: {Content}",
                        name, msg.Sender,
                        msg.Content.Length > 30 ? msg.Content[..30] + "..." : msg.Content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "채팅방 '{ChatRoom}' 메시지 폴링 중 오류", name);
            }
        }

        // 첫 폴링이 모든 채팅방에 대해 완료됨
        if (_firstPoll)
        {
            _firstPoll = false;
            _logger.LogInformation("기존 메시지 초기화 완료, 이후부터 새 메시지를 감지합니다.");
        }
    }
}
