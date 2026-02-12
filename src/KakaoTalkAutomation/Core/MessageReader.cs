using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using KakaoTalkAutomation.Helpers;
using Microsoft.Extensions.Logging;

namespace KakaoTalkAutomation.Core;

/// <summary>
/// 카카오톡 채팅방에서 메시지를 읽어오는 클래스
///
/// 카카오톡은 커스텀 컨트롤(EVA_VH_ListControl_Dblclk)을 사용하여
/// 표준 UI Automation으로 메시지 텍스트에 직접 접근할 수 없습니다.
///
/// 따라서 메시지 목록 영역에 포커스를 주고
/// Ctrl+A(전체 선택) → Ctrl+C(복사) 후 클립보드에서 텍스트를 읽어옵니다.
///
/// 클립보드 텍스트 형식 예시:
///   [홍길동] [오후 1:30] 안녕하세요
///   [홍길동] [오후 1:31] 반갑습니다
///   [나] [오후 1:32] 네 안녕하세요!
/// </summary>
public class MessageReader : IDisposable
{
    private readonly ILogger<MessageReader> _logger;
    private readonly UIA3Automation _automation;

    /// <summary>
    /// 채팅방별 마지막으로 읽은 메시지 해시를 추적합니다.
    /// </summary>
    private readonly Dictionary<IntPtr, HashSet<string>> _knownMessageHashes = new();

    // Win32 API
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // 키 상수
    private const byte VK_CONTROL = 0x11;
    private const byte VK_A = 0x41;
    private const byte VK_C = 0x43;
    private const byte VK_ESCAPE = 0x1B;
    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public MessageReader(ILogger<MessageReader> logger)
    {
        _logger = logger;
        _automation = new UIA3Automation();
    }

    /// <summary>
    /// 채팅방 창의 UI 구조를 진단하여 콘솔에 출력합니다.
    /// </summary>
    public void DiagnoseUIStructure(IntPtr chatRoomHandle)
    {
        try
        {
            var window = _automation.FromHandle(chatRoomHandle);
            if (window == null)
            {
                Console.WriteLine("  [오류] 창 요소를 가져올 수 없습니다.");
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== 카카오톡 채팅방 UI 구조 진단 ===\n");
            Console.ResetColor();

            // 1단계: 직계 자식 요소
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  [1단계] 직계 자식 요소:");
            Console.ResetColor();

            var children = window.FindAllChildren();
            for (int i = 0; i < children.Length; i++)
            {
                PrintElementInfo(children[i], $"    [{i}]");
            }

            // 2단계: Win32 자식 창 열거
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  [2단계] Win32 자식 창:");
            Console.ResetColor();

            int childIndex = 0;
            Win32Api.EnumChildWindows(chatRoomHandle, (hWnd, lParam) =>
            {
                var className = Win32Api.GetWindowClassName(hWnd);
                var title = Win32Api.GetWindowTitle(hWnd);
                Console.WriteLine($"    [{childIndex++}] Class=\"{className}\" | 핸들=0x{hWnd:X} | Title=\"{title}\"");
                return true;
            }, IntPtr.Zero);

            // 3단계: 클립보드 기반 메시지 읽기 테스트
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  [3단계] 클립보드 기반 메시지 읽기 테스트:");
            Console.ResetColor();

            var clipText = CaptureMessagesViaClipboard(chatRoomHandle);
            if (string.IsNullOrEmpty(clipText))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("    클립보드에서 텍스트를 가져오지 못했습니다.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"    클립보드 텍스트 길이: {clipText.Length}자");
                Console.ResetColor();

                // 마지막 5줄만 출력
                var lines = clipText.Split('\n');
                var showCount = Math.Min(lines.Length, 10);
                Console.WriteLine($"    총 {lines.Length}줄 (마지막 {showCount}줄 표시):");
                for (int i = Math.Max(0, lines.Length - showCount); i < lines.Length; i++)
                {
                    var line = lines[i].TrimEnd('\r');
                    var trimmed = line.Length > 80 ? line[..80] + "..." : line;
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"      {trimmed}");
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== 진단 완료 ===\n");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  진단 중 오류: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// 채팅방에서 모든 메시지를 읽어옵니다.
    /// 클립보드 기반 방식을 사용합니다.
    /// </summary>
    public List<ParsedMessage> ReadMessages(IntPtr chatRoomHandle)
    {
        var messages = new List<ParsedMessage>();

        try
        {
            var clipText = CaptureMessagesViaClipboard(chatRoomHandle);
            if (string.IsNullOrEmpty(clipText))
            {
                _logger.LogWarning("클립보드에서 메시지를 가져오지 못했습니다.");
                return messages;
            }

            messages = ParseClipboardText(clipText);
            _logger.LogDebug("메시지 {Count}개 파싱 완료", messages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "메시지 읽기 중 오류 발생");
        }

        return messages;
    }

    /// <summary>
    /// 새로 수신된 메시지만 반환합니다.
    /// </summary>
    public List<ParsedMessage> ReadNewMessages(IntPtr chatRoomHandle)
    {
        var allMessages = ReadMessages(chatRoomHandle);
        var newMessages = new List<ParsedMessage>();

        if (!_knownMessageHashes.TryGetValue(chatRoomHandle, out var knownHashes))
        {
            knownHashes = new HashSet<string>();
            _knownMessageHashes[chatRoomHandle] = knownHashes;
        }

        foreach (var msg in allMessages)
        {
            var hash = $"{msg.Sender}|{msg.Content}|{msg.Timestamp:HHmm}";
            if (knownHashes.Add(hash))
            {
                newMessages.Add(msg);
            }
        }

        if (newMessages.Count > 0)
        {
            _logger.LogInformation("새 메시지 {Count}개 감지됨", newMessages.Count);
        }

        // 해시 세트가 너무 커지면 정리
        if (knownHashes.Count > 5000)
        {
            _knownMessageHashes[chatRoomHandle] = new HashSet<string>(
                knownHashes.Skip(knownHashes.Count - 3000));
        }

        return newMessages;
    }

    /// <summary>
    /// 기존 메시지를 모두 "이미 읽음" 처리합니다.
    /// 모니터링 시작 시 호출합니다.
    /// </summary>
    public void InitializeMessageCount(IntPtr chatRoomHandle)
    {
        var allMessages = ReadMessages(chatRoomHandle);
        var hashes = new HashSet<string>();

        foreach (var msg in allMessages)
        {
            var hash = $"{msg.Sender}|{msg.Content}|{msg.Timestamp:HHmm}";
            hashes.Add(hash);
        }

        _knownMessageHashes[chatRoomHandle] = hashes;
        _logger.LogInformation("채팅방 메시지 초기화: {Count}개 (이미 읽음 처리)", allMessages.Count);
    }

    // 마우스/좌표 Win32 API
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    /// <summary>
    /// 카카오톡 채팅방의 메시지 목록 영역에서 텍스트를 클립보드로 복사합니다.
    ///
    /// 동작 흐름:
    ///   1. 채팅방 창을 포그라운드로 가져옴
    ///   2. 메시지 목록 영역(EVA_VH_ListControl_Dblclk)을 마우스 클릭하여 포커스
    ///   3. Ctrl+A (전체 선택) → Ctrl+C (복사)
    ///   4. 클립보드에서 텍스트 읽기
    ///
    /// ※ Escape 키는 절대 보내지 않음 (카카오톡에서 ESC = 창 닫기)
    /// </summary>
    private string? CaptureMessagesViaClipboard(IntPtr chatRoomHandle)
    {
        try
        {
            // 기존 클립보드 내용 백업
            string? previousClipboard = GetClipboardText();

            // 1. 창 활성화
            ForceActivateWindow(chatRoomHandle);
            Thread.Sleep(400);

            // 2. 메시지 목록 영역을 마우스 클릭하여 포커스
            var messageListHandle = FindMessageListControl(chatRoomHandle);
            if (messageListHandle != IntPtr.Zero)
            {
                ClickOnControl(messageListHandle);
                Thread.Sleep(300);
            }
            else
            {
                _logger.LogWarning("메시지 목록 컨트롤을 찾지 못했습니다.");
                // 채팅방 창의 중앙 상단 영역을 클릭 (메시지 목록은 보통 위쪽에 위치)
                ClickOnWindowCenter(chatRoomHandle, yOffsetRatio: 0.3);
                Thread.Sleep(300);
            }

            // 3. Ctrl+A (전체 선택)
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event(VK_A, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event(VK_A, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(500); // 선택 완료 대기 (충분히)

            // 4. Ctrl+C (복사)
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event(VK_C, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(500); // 복사 완료 대기

            // 5. 클립보드에서 텍스트 읽기
            var clipText = GetClipboardText();

            // 6. 메시지 목록 아무 곳이나 한번 더 클릭 (선택 해제 - ESC 대신)
            if (messageListHandle != IntPtr.Zero)
            {
                ClickOnControl(messageListHandle);
            }

            // 클립보드 내용 확인
            if (string.IsNullOrWhiteSpace(clipText))
            {
                _logger.LogDebug("클립보드가 비어있음 - 복사 실패");
                return null;
            }

            if (clipText == previousClipboard)
            {
                _logger.LogDebug("클립보드 내용 변화 없음 - 복사 실패");
                return null;
            }

            _logger.LogDebug("클립보드에서 {Length}자 읽기 성공", clipText.Length);
            return clipText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "클립보드 기반 메시지 캡처 실패");
            return null;
        }
    }

    /// <summary>
    /// Win32 컨트롤의 중앙을 마우스 클릭합니다.
    /// </summary>
    private void ClickOnControl(IntPtr controlHandle)
    {
        if (GetWindowRect(controlHandle, out RECT rect))
        {
            // 마우스 위치 백업
            GetCursorPos(out POINT savedPos);

            // 컨트롤 중앙 좌표
            int centerX = (rect.Left + rect.Right) / 2;
            int centerY = (rect.Top + rect.Bottom) / 2;

            // 마우스 이동 → 클릭
            SetCursorPos(centerX, centerY);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(30);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);

            // 마우스 위치 복원
            SetCursorPos(savedPos.X, savedPos.Y);

            _logger.LogDebug("컨트롤 클릭 - 좌표: ({X}, {Y})", centerX, centerY);
        }
        else
        {
            _logger.LogDebug("컨트롤 좌표를 가져올 수 없습니다: 0x{Handle:X}", controlHandle);
        }
    }

    /// <summary>
    /// 창의 특정 비율 위치를 마우스 클릭합니다.
    /// </summary>
    private void ClickOnWindowCenter(IntPtr windowHandle, double yOffsetRatio = 0.5)
    {
        if (GetWindowRect(windowHandle, out RECT rect))
        {
            GetCursorPos(out POINT savedPos);

            int centerX = (rect.Left + rect.Right) / 2;
            int targetY = rect.Top + (int)((rect.Bottom - rect.Top) * yOffsetRatio);

            SetCursorPos(centerX, targetY);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(30);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);

            SetCursorPos(savedPos.X, savedPos.Y);
        }
    }

    /// <summary>
    /// 카카오톡의 메시지 목록 컨트롤 핸들을 찾습니다.
    /// Class: EVA_VH_ListControl_Dblclk
    /// </summary>
    private IntPtr FindMessageListControl(IntPtr chatRoomHandle)
    {
        IntPtr listHandle = IntPtr.Zero;

        Win32Api.EnumChildWindows(chatRoomHandle, (hWnd, lParam) =>
        {
            var className = Win32Api.GetWindowClassName(hWnd);
            if (className == "EVA_VH_ListControl_Dblclk")
            {
                listHandle = hWnd;
                return false; // 찾으면 중단
            }
            return true;
        }, IntPtr.Zero);

        if (listHandle != IntPtr.Zero)
        {
            _logger.LogDebug("메시지 목록 컨트롤 발견: 0x{Handle:X}", listHandle);
        }
        else
        {
            _logger.LogDebug("EVA_VH_ListControl_Dblclk 컨트롤을 찾지 못함");
        }

        return listHandle;
    }

    // ========================================
    // 클립보드 텍스트 파싱
    // ========================================

    /// <summary>
    /// 카카오톡에서 복사한 클립보드 텍스트를 파싱하여 메시지 목록을 생성합니다.
    ///
    /// 카카오톡 복사 형식 예시:
    ///   [홍길동] [오후 1:30] 안녕하세요
    ///   [홍길동] [오후 1:31] 여러 줄
    ///   메시지도 가능합니다
    ///
    /// 정규식 패턴: \[(.+?)\] \[(오전|오후) (\d{1,2}:\d{2})\] (.+)
    /// </summary>
    private List<ParsedMessage> ParseClipboardText(string text)
    {
        var messages = new List<ParsedMessage>();

        // 카카오톡 메시지 패턴: [이름] [오전/오후 시:분] 내용
        var pattern = @"^\[(.+?)\] \[(오전|오후) (\d{1,2}:\d{2})\] (.+)$";
        var regex = new Regex(pattern, RegexOptions.Multiline);

        var lines = text.Split('\n');
        ParsedMessage? currentMessage = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // 날짜 구분선 건너뛰기 (예: "---- 2026년 2월 12일 수요일 ----", "2026년 2월 12일 수요일")
            if (IsDateSeparator(line)) continue;

            // 빈 줄 건너뛰기
            if (string.IsNullOrWhiteSpace(line)) continue;

            var match = regex.Match(line);
            if (match.Success)
            {
                // 이전 메시지 저장
                if (currentMessage != null)
                {
                    messages.Add(currentMessage);
                }

                var sender = match.Groups[1].Value;
                var ampm = match.Groups[2].Value;
                var time = match.Groups[3].Value;
                var content = match.Groups[4].Value;

                currentMessage = new ParsedMessage
                {
                    Sender = sender,
                    Content = content,
                    Timestamp = ParseKakaoTime(ampm, time)
                };
            }
            else if (currentMessage != null)
            {
                // 패턴에 안 맞는 줄은 이전 메시지의 연속 (여러 줄 메시지)
                currentMessage.Content += "\n" + line;
            }
        }

        // 마지막 메시지 저장
        if (currentMessage != null)
        {
            messages.Add(currentMessage);
        }

        return messages;
    }

    /// <summary>
    /// 날짜 구분선 여부를 확인합니다.
    /// 예: "---- 2026년 2월 12일 수요일 ----"
    ///     "2026년 2월 12일 수요일"
    /// </summary>
    private static bool IsDateSeparator(string line)
    {
        line = line.Trim().Trim('-').Trim();

        // "YYYY년 M월 D일 요일" 패턴
        if (Regex.IsMatch(line, @"\d{4}년\s+\d{1,2}월\s+\d{1,2}일"))
            return true;

        return false;
    }

    /// <summary>
    /// 카카오톡 시간 형식 ("오전"/"오후", "HH:MM")을 DateTime으로 변환합니다.
    /// </summary>
    private static DateTime ParseKakaoTime(string ampm, string time)
    {
        try
        {
            var parts = time.Split(':');
            int hour = int.Parse(parts[0]);
            int minute = int.Parse(parts[1]);

            if (ampm == "오후" && hour != 12) hour += 12;
            else if (ampm == "오전" && hour == 12) hour = 0;

            return DateTime.Today.AddHours(hour).AddMinutes(minute);
        }
        catch
        {
            return DateTime.Now;
        }
    }

    // ========================================
    // 유틸리티  
    // ========================================

    /// <summary>
    /// 창을 강제로 포그라운드로 가져옵니다.
    /// </summary>
    private void ForceActivateWindow(IntPtr hWnd)
    {
        var foregroundWindow = GetForegroundWindow();
        var currentThreadId = GetCurrentThreadId();
        var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
        var targetThreadId = GetWindowThreadProcessId(hWnd, out _);

        if (currentThreadId != foregroundThreadId)
            AttachThreadInput(currentThreadId, foregroundThreadId, true);
        if (currentThreadId != targetThreadId)
            AttachThreadInput(currentThreadId, targetThreadId, true);

        Win32Api.ShowWindow(hWnd, Win32Api.SW_RESTORE);
        Thread.Sleep(100);
        SetForegroundWindow(hWnd);
        Thread.Sleep(100);

        if (currentThreadId != foregroundThreadId)
            AttachThreadInput(currentThreadId, foregroundThreadId, false);
        if (currentThreadId != targetThreadId)
            AttachThreadInput(currentThreadId, targetThreadId, false);
    }

    /// <summary>
    /// 클립보드에서 텍스트를 읽어옵니다.
    /// </summary>
    private static string? GetClipboardText()
    {
        string? result = null;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Forms.Clipboard.ContainsText())
                {
                    result = System.Windows.Forms.Clipboard.GetText(System.Windows.Forms.TextDataFormat.UnicodeText);
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(3000);

        return result;
    }

    /// <summary>
    /// UI 요소 정보를 콘솔에 출력합니다. (진단용)
    /// </summary>
    private static void PrintElementInfo(AutomationElement el, string indent)
    {
        try
        {
            var name = el.Name ?? "(없음)";
            var trimmedName = name.Length > 50 ? name[..50] + "..." : name;
            var className = el.ClassName ?? "(없음)";
            var controlType = el.ControlType;
            var automationId = el.AutomationId ?? "";

            Console.Write($"{indent} ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{controlType}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($" | Class=\"{className}\"");

            if (!string.IsNullOrEmpty(automationId))
                Console.Write($" | Id=\"{automationId}\"");

            if (!string.IsNullOrEmpty(name) && name != "(없음)")
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($" | Name=\"{trimmedName}\"");
            }

            Console.ResetColor();
            Console.WriteLine();
        }
        catch
        {
            Console.WriteLine($"{indent} [요소 정보 읽기 실패]");
        }
    }

    public void Dispose()
    {
        _automation?.Dispose();
    }
}

/// <summary>
/// 파싱된 메시지 데이터 모델
/// </summary>
public class ParsedMessage
{
    /// <summary>보낸 사람</summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>메시지 내용</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>메시지 시간</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
