using System.Diagnostics;
using System.Text;
using KakaoTalkAutomation.Helpers;
using Microsoft.Extensions.Logging;

namespace KakaoTalkAutomation.Core;

/// <summary>
/// 카카오톡 PC 앱의 창을 탐색하고 핸들을 획득하는 클래스
/// Win32 API를 사용하여 카카오톡 메인 창 및 채팅방 창을 찾습니다.
/// </summary>
public class KakaoTalkFinder
{
    private readonly ILogger<KakaoTalkFinder> _logger;

    // 카카오톡 PC 앱의 알려진 클래스명들
    // 참고: 카카오톡 버전에 따라 클래스명이 달라질 수 있습니다.
    private static readonly string[] KakaoMainClassNames = new[]
    {
        "EVA_Window_Dblclk",  // 카카오톡 메인 창 (일반적)
        "EVA_Window",          // 카카오톡 메인 창 (구버전)
        "#32770"               // 대화 상자 형태의 채팅방
    };

    // 카카오톡 채팅방 창의 클래스명
    private static readonly string[] KakaoChatClassNames = new[]
    {
        "EVA_Window_Dblclk",   // 채팅방 창
        "#32770"               // 채팅방 팝업
    };

    /// <summary>
    /// 카카오톡 프로세스명
    /// </summary>
    private const string KakaoProcessName = "KakaoTalk";

    public KakaoTalkFinder(ILogger<KakaoTalkFinder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 카카오톡 프로세스가 실행 중인지 확인합니다.
    /// </summary>
    /// <returns>실행 중이면 true</returns>
    public bool IsKakaoTalkRunning()
    {
        var processes = Process.GetProcessesByName(KakaoProcessName);
        var isRunning = processes.Length > 0;

        if (isRunning)
        {
            _logger.LogDebug("카카오톡 프로세스 발견: {Count}개", processes.Length);
        }
        else
        {
            _logger.LogWarning("카카오톡 프로세스를 찾을 수 없습니다.");
        }

        return isRunning;
    }

    /// <summary>
    /// 카카오톡 메인 창 핸들을 찾습니다.
    /// </summary>
    /// <returns>메인 창 핸들, 못 찾으면 IntPtr.Zero</returns>
    public IntPtr FindMainWindow()
    {
        // 카카오톡 프로세스 ID 가져오기
        var kakaoProcesses = Process.GetProcessesByName(KakaoProcessName);
        if (kakaoProcesses.Length == 0)
        {
            _logger.LogWarning("카카오톡이 실행 중이 아닙니다.");
            return IntPtr.Zero;
        }

        var processIds = new HashSet<uint>(kakaoProcesses.Select(p => (uint)p.Id));
        IntPtr mainWindow = IntPtr.Zero;

        // 모든 최상위 창을 열거하여 카카오톡 메인 창 찾기
        Win32Api.EnumWindows((hWnd, lParam) =>
        {
            Win32Api.GetWindowThreadProcessId(hWnd, out uint processId);

            if (processIds.Contains(processId) && Win32Api.IsWindowVisible(hWnd))
            {
                var className = Win32Api.GetWindowClassName(hWnd);
                var title = Win32Api.GetWindowTitle(hWnd);

                // 메인 창은 일반적으로 "카카오톡" 제목을 가짐
                if (title.Contains("카카오톡") && KakaoMainClassNames.Contains(className))
                {
                    mainWindow = hWnd;
                    _logger.LogInformation("카카오톡 메인 창 발견 - 핸들: {Handle}, 제목: {Title}, 클래스: {Class}",
                        hWnd, title, className);
                    return false; // 열거 중단
                }
            }

            return true; // 계속 열거
        }, IntPtr.Zero);

        // 메인 창을 못 찾으면, 제목에 관계없이 카카오톡 프로세스의 첫 번째 보이는 창을 사용
        if (mainWindow == IntPtr.Zero)
        {
            Win32Api.EnumWindows((hWnd, lParam) =>
            {
                Win32Api.GetWindowThreadProcessId(hWnd, out uint processId);

                if (processIds.Contains(processId) && Win32Api.IsWindowVisible(hWnd))
                {
                    mainWindow = hWnd;
                    var title = Win32Api.GetWindowTitle(hWnd);
                    _logger.LogInformation("카카오톡 창 발견 (대체) - 핸들: {Handle}, 제목: {Title}", hWnd, title);
                    return false;
                }

                return true;
            }, IntPtr.Zero);
        }

        return mainWindow;
    }

    /// <summary>
    /// 현재 열려 있는 카카오톡 채팅방 창 목록을 반환합니다.
    /// </summary>
    /// <returns>(창 핸들, 채팅방 이름) 튜플 목록</returns>
    public List<(IntPtr Handle, string Name)> FindChatRooms()
    {
        var chatRooms = new List<(IntPtr Handle, string Name)>();
        var kakaoProcesses = Process.GetProcessesByName(KakaoProcessName);

        if (kakaoProcesses.Length == 0)
        {
            _logger.LogWarning("카카오톡이 실행 중이 아닙니다.");
            return chatRooms;
        }

        var processIds = new HashSet<uint>(kakaoProcesses.Select(p => (uint)p.Id));

        Win32Api.EnumWindows((hWnd, lParam) =>
        {
            Win32Api.GetWindowThreadProcessId(hWnd, out uint processId);

            if (processIds.Contains(processId) && Win32Api.IsWindowVisible(hWnd))
            {
                var title = Win32Api.GetWindowTitle(hWnd);
                var className = Win32Api.GetWindowClassName(hWnd);

                // 메인 창("카카오톡"), 빈 제목, 알림 창 등은 제외
                if (!string.IsNullOrEmpty(title) &&
                    title != "카카오톡" &&
                    !title.StartsWith("KakaoTalk") &&
                    KakaoChatClassNames.Contains(className))
                {
                    chatRooms.Add((hWnd, title));
                    _logger.LogDebug("채팅방 발견 - 핸들: {Handle}, 이름: {Name}, 클래스: {Class}",
                        hWnd, title, className);
                }
            }

            return true; // 모든 창 열거
        }, IntPtr.Zero);

        _logger.LogInformation("발견된 채팅방 수: {Count}", chatRooms.Count);
        return chatRooms;
    }

    /// <summary>
    /// 특정 이름의 채팅방 창 핸들을 찾습니다.
    /// </summary>
    /// <param name="chatRoomName">채팅방 이름 (부분 일치 검색)</param>
    /// <returns>채팅방 창 핸들, 못 찾으면 IntPtr.Zero</returns>
    public IntPtr FindChatRoomByName(string chatRoomName)
    {
        var chatRooms = FindChatRooms();
        var found = chatRooms.FirstOrDefault(c =>
            c.Name.Contains(chatRoomName, StringComparison.OrdinalIgnoreCase));

        if (found.Handle != IntPtr.Zero)
        {
            _logger.LogInformation("채팅방 '{Name}' 발견 - 핸들: {Handle}", found.Name, found.Handle);
        }
        else
        {
            _logger.LogWarning("채팅방 '{Name}'을 찾을 수 없습니다.", chatRoomName);
        }

        return found.Handle;
    }

    /// <summary>
    /// 채팅방 창을 포그라운드로 활성화합니다.
    /// </summary>
    /// <param name="chatRoomHandle">채팅방 창 핸들</param>
    public void ActivateChatRoom(IntPtr chatRoomHandle)
    {
        if (chatRoomHandle == IntPtr.Zero)
        {
            _logger.LogWarning("유효하지 않은 창 핸들입니다.");
            return;
        }

        // 최소화된 창이면 복원
        Win32Api.ShowWindow(chatRoomHandle, Win32Api.SW_RESTORE);
        // 포그라운드로 가져오기
        Win32Api.SetForegroundWindow(chatRoomHandle);
        _logger.LogDebug("채팅방 창 활성화 완료 - 핸들: {Handle}", chatRoomHandle);
    }
}
