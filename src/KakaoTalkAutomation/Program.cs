using KakaoTalkAutomation.Core;
using KakaoTalkAutomation.Data;
using KakaoTalkAutomation.Data.Repositories;
using KakaoTalkAutomation.Helpers;
using KakaoTalkAutomation.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace KakaoTalkAutomation;

/// <summary>
/// 카카오톡 메시지 자동화 프로그램 진입점
/// 콘솔 기반 CLI 인터페이스를 제공합니다.
/// </summary>
public class Program
{
    private static IHost? _host;
    private static MessageMonitorService? _monitorService;
    private static MessageRepository? _repository;
    private static KakaoTalkFinder? _finder;
    private static MessageReader? _reader;

    public static async Task Main(string[] args)
    {
        // 콘솔 인코딩을 UTF-8로 설정 (한글 출력을 위해)
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        // Serilog 설정
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.File("logs/kakaotalk-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            // Host 설정 (DI 컨테이너 구성)
            _host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    // DB 컨텍스트 등록
                    var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kakaotalk_messages.db");
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseSqlite($"Data Source={dbPath}"));

                    // Core 모듈 등록
                    services.AddSingleton<KakaoTalkFinder>();
                    services.AddSingleton<MessageReader>();
                    services.AddSingleton<MessageSender>();

                    // 데이터 계층 등록
                    services.AddScoped<MessageRepository>();

                    // 서비스 등록
                    services.AddSingleton<MessageMonitorService>();
                    services.AddScoped<MessageService>();

                    // 백그라운드 서비스 등록
                    services.AddHostedService(sp => sp.GetRequiredService<MessageMonitorService>());
                })
                .Build();

            // 호스트 시작 (백그라운드 서비스 시작)
            await _host.StartAsync();

            // 서비스 인스턴스 가져오기
            _monitorService = _host.Services.GetRequiredService<MessageMonitorService>();
            _finder = _host.Services.GetRequiredService<KakaoTalkFinder>();
            _reader = _host.Services.GetRequiredService<MessageReader>();

            // DB 초기화
            using (var scope = _host.Services.CreateScope())
            {
                _repository = scope.ServiceProvider.GetRequiredService<MessageRepository>();
                await _repository.InitializeDatabaseAsync();
            }

            // 메인 루프 실행
            ConsoleHelper.PrintHeader();
            await RunMainLoop();

            // 호스트 정상 종료
            await _host.StopAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "프로그램 실행 중 치명적 오류 발생");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"치명적 오류: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// 메인 CLI 루프
    /// </summary>
    private static async Task RunMainLoop()
    {
        while (true)
        {
            try
            {
                ConsoleHelper.PrintMenu();
                var input = Console.ReadLine()?.Trim();

                switch (input)
                {
                    case "1":
                        await HandleMonitoring();
                        break;
                    case "2":
                        await HandleSendMessage();
                        break;
                    case "3":
                        await HandleViewMessages();
                        break;
                    case "4":
                        HandleListChatRooms();
                        break;
                    case "5":
                        HandleCheckConnection();
                        break;
                    case "6":
                        HandleDiagnoseUI();
                        break;
                    case "7":
                        HandleCaptureTest();
                        break;
                    case "0":
                        ConsoleHelper.PrintInfo("프로그램을 종료합니다...");
                        return;
                    default:
                        ConsoleHelper.PrintWarning("올바른 메뉴 번호를 입력해주세요.");
                        break;
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintError($"오류 발생: {ex.Message}");
                Log.Error(ex, "메뉴 처리 중 오류");
            }
        }
    }

    /// <summary>
    /// 메뉴 1: 메시지 모니터링 시작/중지
    /// </summary>
    private static Task HandleMonitoring()
    {
        if (_monitorService == null) return Task.CompletedTask;

        if (_monitorService.IsMonitoring)
        {
            _monitorService.StopMonitoring();
            ConsoleHelper.PrintSuccess("모니터링이 중지되었습니다.");
        }
        else
        {
            ConsoleHelper.PrintSeparator();
            Console.WriteLine("모니터링 설정:");
            Console.WriteLine("  1. 모든 열린 채팅방 모니터링");
            Console.WriteLine("  2. 특정 채팅방만 모니터링");
            Console.Write("선택: ");

            var choice = Console.ReadLine()?.Trim();

            if (choice == "2")
            {
                var roomName = ConsoleHelper.ReadInput("모니터링할 채팅방 이름 (쉼표로 구분)");
                var rooms = roomName.Split(',').Select(r => r.Trim()).Where(r => !string.IsNullOrEmpty(r)).ToList();

                if (rooms.Count == 0)
                {
                    ConsoleHelper.PrintWarning("채팅방 이름을 입력해주세요.");
                    return Task.CompletedTask;
                }

                // 새 메시지 수신 이벤트 핸들러 등록
                _monitorService.OnNewMessageReceived += msg =>
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"\n  📩 새 메시지 - [{msg.ChatRoomName}] {msg.Sender}: {msg.Content}");
                    Console.ResetColor();
                    Console.Write("선택: ");
                };

                _monitorService.StartMonitoring(rooms);
                ConsoleHelper.PrintSuccess($"채팅방 [{string.Join(", ", rooms)}] 모니터링을 시작합니다.");
            }
            else
            {
                _monitorService.OnNewMessageReceived += msg =>
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"\n  📩 새 메시지 - [{msg.ChatRoomName}] {msg.Sender}: {msg.Content}");
                    Console.ResetColor();
                    Console.Write("선택: ");
                };

                _monitorService.StartMonitoring();
                ConsoleHelper.PrintSuccess("모든 열린 채팅방 모니터링을 시작합니다.");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 메뉴 2: 메시지 보내기
    /// </summary>
    private static async Task HandleSendMessage()
    {
        if (_host == null || _finder == null) return;

        // 열린 채팅방 목록 표시
        var chatRooms = _finder.FindChatRooms();

        if (chatRooms.Count == 0)
        {
            ConsoleHelper.PrintWarning("열린 채팅방이 없습니다. 카카오톡에서 채팅방을 열어주세요.");
            return;
        }

        ConsoleHelper.PrintSeparator();
        Console.WriteLine("열린 채팅방 목록:");
        for (int i = 0; i < chatRooms.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {chatRooms[i].Name}");
        }
        Console.WriteLine();

        var roomInput = ConsoleHelper.ReadInput("채팅방 번호 또는 이름");

        string targetRoom;
        if (int.TryParse(roomInput, out int roomIndex) && roomIndex >= 1 && roomIndex <= chatRooms.Count)
        {
            targetRoom = chatRooms[roomIndex - 1].Name;
        }
        else
        {
            targetRoom = roomInput;
        }

        var message = ConsoleHelper.ReadInput("보낼 메시지");

        if (string.IsNullOrEmpty(message))
        {
            ConsoleHelper.PrintWarning("메시지를 입력해주세요.");
            return;
        }

        // 확인
        Console.WriteLine($"\n  채팅방: {targetRoom}");
        Console.WriteLine($"  메시지: {message}");

        if (!ConsoleHelper.Confirm("전송하시겠습니까?"))
        {
            ConsoleHelper.PrintInfo("전송이 취소되었습니다.");
            return;
        }

        using var scope = _host.Services.CreateScope();
        var messageService = scope.ServiceProvider.GetRequiredService<MessageService>();
        var success = await messageService.SendAndRecordMessageAsync(targetRoom, message);

        if (success)
        {
            ConsoleHelper.PrintSuccess($"'{targetRoom}'에 메시지 전송 완료!");
        }
        else
        {
            ConsoleHelper.PrintError("메시지 전송에 실패했습니다. 채팅방이 열려 있는지 확인해주세요.");
        }
    }

    /// <summary>
    /// 메뉴 3: 저장된 메시지 조회
    /// </summary>
    private static async Task HandleViewMessages()
    {
        if (_host == null) return;

        ConsoleHelper.PrintSeparator();
        Console.WriteLine("메시지 조회 옵션:");
        Console.WriteLine("  1. 최근 메시지 보기");
        Console.WriteLine("  2. 채팅방별 메시지 보기");
        Console.WriteLine("  3. 키워드 검색");
        Console.Write("선택: ");

        var choice = Console.ReadLine()?.Trim();

        using var scope = _host.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<MessageRepository>();

        switch (choice)
        {
            case "1":
                var recentMessages = await repository.GetRecentMessagesAsync(20);
                PrintMessages(recentMessages, "최근 메시지");
                break;

            case "2":
                var savedRooms = await repository.GetChatRoomNamesAsync();
                if (savedRooms.Count == 0)
                {
                    ConsoleHelper.PrintInfo("저장된 채팅방이 없습니다.");
                    return;
                }

                Console.WriteLine("저장된 채팅방:");
                for (int i = 0; i < savedRooms.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {savedRooms[i]}");
                }

                var roomInput = ConsoleHelper.ReadInput("채팅방 번호 또는 이름");
                string targetRoom;

                if (int.TryParse(roomInput, out int idx) && idx >= 1 && idx <= savedRooms.Count)
                {
                    targetRoom = savedRooms[idx - 1];
                }
                else
                {
                    targetRoom = roomInput;
                }

                var roomMessages = await repository.GetMessagesByChatRoomAsync(targetRoom, 50);
                PrintMessages(roomMessages, $"채팅방: {targetRoom}");
                break;

            case "3":
                var keyword = ConsoleHelper.ReadInput("검색 키워드");
                if (string.IsNullOrEmpty(keyword))
                {
                    ConsoleHelper.PrintWarning("키워드를 입력해주세요.");
                    return;
                }

                var searchResults = await repository.SearchMessagesAsync(keyword, 50);
                PrintMessages(searchResults, $"검색 결과: '{keyword}'");
                break;

            default:
                ConsoleHelper.PrintWarning("올바른 옵션을 선택해주세요.");
                break;
        }
    }

    /// <summary>
    /// 메뉴 4: 채팅방 목록 보기
    /// </summary>
    private static void HandleListChatRooms()
    {
        if (_finder == null) return;

        var chatRooms = _finder.FindChatRooms();

        ConsoleHelper.PrintSeparator();
        if (chatRooms.Count == 0)
        {
            ConsoleHelper.PrintWarning("열린 채팅방이 없습니다.");
            ConsoleHelper.PrintInfo("카카오톡에서 채팅방 창을 열어주세요.");
        }
        else
        {
            Console.WriteLine($"열린 채팅방 ({chatRooms.Count}개):");
            for (int i = 0; i < chatRooms.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {chatRooms[i].Name} (핸들: 0x{chatRooms[i].Handle:X})");
            }
        }
    }

    /// <summary>
    /// 메뉴 5: 카카오톡 연결 상태 확인
    /// </summary>
    private static void HandleCheckConnection()
    {
        if (_host == null || _finder == null) return;

        ConsoleHelper.PrintSeparator();
        Console.WriteLine("카카오톡 연결 상태:");

        // 프로세스 확인
        var isRunning = _finder.IsKakaoTalkRunning();
        if (isRunning)
        {
            ConsoleHelper.PrintSuccess("카카오톡 프로세스 실행 중");
        }
        else
        {
            ConsoleHelper.PrintError("카카오톡이 실행 중이 아닙니다!");
            return;
        }

        // 메인 창 확인
        var mainWindow = _finder.FindMainWindow();
        if (mainWindow != IntPtr.Zero)
        {
            ConsoleHelper.PrintSuccess($"메인 창 발견 (핸들: 0x{mainWindow:X})");
        }
        else
        {
            ConsoleHelper.PrintWarning("메인 창을 찾을 수 없습니다.");
        }

        // 채팅방 확인
        var chatRooms = _finder.FindChatRooms();
        ConsoleHelper.PrintInfo($"열린 채팅방: {chatRooms.Count}개");

        // 모니터링 상태
        if (_monitorService != null)
        {
            if (_monitorService.IsMonitoring)
            {
                ConsoleHelper.PrintSuccess("모니터링 활성화 중");
            }
            else
            {
                ConsoleHelper.PrintInfo("모니터링 비활성화 상태");
            }
        }
    }

    /// <summary>
    /// 메뉴 6: UI 구조 진단 (디버그용)
    /// 카카오톡 채팅방의 UI 요소 구조를 분석하여 메시지 읽기 전략을 최적화하는 데 사용합니다.
    /// </summary>
    private static void HandleDiagnoseUI()
    {
        if (_finder == null || _reader == null) return;

        var chatRooms = _finder.FindChatRooms();

        if (chatRooms.Count == 0)
        {
            ConsoleHelper.PrintWarning("열린 채팅방이 없습니다. 카카오톡에서 채팅방을 열어주세요.");
            return;
        }

        ConsoleHelper.PrintSeparator();
        Console.WriteLine("열린 채팅방 목록:");
        for (int i = 0; i < chatRooms.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {chatRooms[i].Name}");
        }

        var roomInput = ConsoleHelper.ReadInput("진단할 채팅방 번호");
        if (!int.TryParse(roomInput, out int roomIndex) || roomIndex < 1 || roomIndex > chatRooms.Count)
        {
            ConsoleHelper.PrintWarning("올바른 번호를 입력해주세요.");
            return;
        }

        var selectedRoom = chatRooms[roomIndex - 1];
        ConsoleHelper.PrintInfo($"'{selectedRoom.Name}' 채팅방 UI 구조를 분석합니다...");
        _reader.DiagnoseUIStructure(selectedRoom.Handle);

        // 메시지 읽기 테스트도 실행
        ConsoleHelper.PrintSeparator();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("=== 메시지 읽기 테스트 ===");
        Console.ResetColor();

        var messages = _reader.ReadMessages(selectedRoom.Handle);
        if (messages.Count == 0)
        {
            ConsoleHelper.PrintWarning("메시지를 읽지 못했습니다. 위 진단 결과를 참고하여 코드를 수정해야 할 수 있습니다.");
        }
        else
        {
            ConsoleHelper.PrintSuccess($"{messages.Count}개 메시지 읽기 성공!");
            var showCount = Math.Min(messages.Count, 10);
            Console.WriteLine($"마지막 {showCount}개 메시지:");
            foreach (var msg in messages.Skip(messages.Count - showCount))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{msg.Timestamp:HH:mm}] ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{msg.Sender}: ");
                Console.ResetColor();
                Console.WriteLine(msg.Content.Length > 60 ? msg.Content[..60] + "..." : msg.Content);
            }
        }
    }

    /// <summary>
    /// 메시지 목록을 콘솔에 출력합니다.
    /// </summary>
    private static void PrintMessages(List<Data.Models.ChatMessage> messages, string title)
    {
        ConsoleHelper.PrintSeparator();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"📋 {title} ({messages.Count}건)");
        Console.ResetColor();
        ConsoleHelper.PrintSeparator();

        if (messages.Count == 0)
        {
            ConsoleHelper.PrintInfo("메시지가 없습니다.");
            return;
        }

        // 최신순 → 시간순으로 뒤집어서 출력
        var ordered = messages.OrderBy(m => m.MessageTime).ToList();

        foreach (var msg in ordered)
        {
            var directionIcon = msg.IsOutgoing ? "→" : "←";
            var directionColor = msg.IsOutgoing ? ConsoleColor.Green : ConsoleColor.White;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"  [{msg.MessageTime:MM-dd HH:mm}] ");

            Console.ForegroundColor = directionColor;
            Console.Write($"{directionIcon} ");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{msg.Sender}: ");

            Console.ResetColor();
            Console.WriteLine(msg.Content);
        }

        ConsoleHelper.PrintSeparator();
    }

    /// <summary>
    /// 메뉴 7: 비활성 캡처 테스트 (OCR 준비용)
    /// 카카오톡 창이 가려져 있거나 뒤에 있을 때도 캡처가 가능한지 테스트합니다.
    /// </summary>
    private static void HandleCaptureTest()
    {
        if (_finder == null) return;

        var chatRooms = _finder.FindChatRooms();

        if (chatRooms.Count == 0)
        {
            ConsoleHelper.PrintWarning("열린 채팅방이 없습니다.");
            return;
        }

        ConsoleHelper.PrintSeparator();
        Console.WriteLine("캡처할 채팅방 선택:");
        for (int i = 0; i < chatRooms.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {chatRooms[i].Name}");
        }

        var roomInput = ConsoleHelper.ReadInput("채팅방 번호");
        if (!int.TryParse(roomInput, out int roomIndex) || roomIndex < 1 || roomIndex > chatRooms.Count)
        {
            ConsoleHelper.PrintWarning("올바른 번호를 입력해주세요.");
            return;
        }

        var selectedRoom = chatRooms[roomIndex - 1];
        ConsoleHelper.PrintInfo($"'{selectedRoom.Name}' 채팅방의 비활성 캡처를 시도합니다...");

        try
        {
            // 캡처 시도
            using var bmp = CaptureHelper.CaptureWindow(selectedRoom.Handle);
            
            if (bmp == null)
            {
                ConsoleHelper.PrintError("캡처 실패 (비트맵 생성 불가)");
                return;
            }

            // 파일로 저장
            var filename = $"Test_{selectedRoom.Name}_{DateTime.Now:HHmmss}";
            // 파일명에 유효하지 않은 문자 제거
            filename = string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
            
            var path = CaptureHelper.SaveCapture(bmp, filename);

            if (!string.IsNullOrEmpty(path))
            {
                ConsoleHelper.PrintSuccess("캡처 성공!");
                Console.WriteLine($"  저장 경로: {path}");
                ConsoleHelper.PrintInfo("해당 이미지를 열어서, 내용이 잘 보이는지(검은 화면이 아닌지) 확인해주세요.");
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.PrintError($"캡처 중 오류: {ex.Message}");
        }
    }
}
