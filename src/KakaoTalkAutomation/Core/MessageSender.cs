using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using KakaoTalkAutomation.Helpers;
using Microsoft.Extensions.Logging;

namespace KakaoTalkAutomation.Core;

/// <summary>
/// 카카오톡 채팅방에 메시지를 보내는 클래스
/// 
/// 전송 전략:
///   1순위: 창 활성화 → 클립보드에 텍스트 복사 → Ctrl+V 붙여넣기 → Enter 전송 (가장 안정적)
///   2순위: Win32 API로 Edit 컨트롤에 직접 텍스트 설정 → Enter 전송
///
/// 카카오톡 입력 필드가 RichEdit 계열이라 WM_SETTEXT가 작동하지 않을 수 있어,
/// 클립보드 기반 붙여넣기를 기본 전략으로 사용합니다.
/// </summary>
public class MessageSender : IDisposable
{
    private readonly ILogger<MessageSender> _logger;
    private readonly KakaoTalkFinder _finder;
    private readonly UIA3Automation _automation;

    // ========================================
    // 키보드 시뮬레이션용 Win32 API
    // ========================================

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

    // 키보드 이벤트 상수
    private const byte VK_RETURN = 0x0D;
    private const byte VK_CONTROL = 0x11;
    private const byte VK_V = 0x56;
    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public MessageSender(ILogger<MessageSender> logger, KakaoTalkFinder finder)
    {
        _logger = logger;
        _finder = finder;
        _automation = new UIA3Automation();
    }

    /// <summary>
    /// 특정 채팅방에 메시지를 보냅니다.
    /// </summary>
    /// <param name="chatRoomName">채팅방 이름</param>
    /// <param name="message">보낼 메시지 내용</param>
    /// <returns>전송 성공 여부</returns>
    public bool SendMessage(string chatRoomName, string message)
    {
        try
        {
            // 1. 채팅방 창 찾기
            var chatRoomHandle = _finder.FindChatRoomByName(chatRoomName);
            if (chatRoomHandle == IntPtr.Zero)
            {
                _logger.LogError("채팅방 '{ChatRoom}'을 찾을 수 없습니다. 채팅방 창이 열려 있는지 확인해주세요.", chatRoomName);
                return false;
            }

            return SendMessageToHandle(chatRoomHandle, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "메시지 전송 중 오류 발생 - 채팅방: {ChatRoom}", chatRoomName);
            return false;
        }
    }

    /// <summary>
    /// 창 핸들을 사용하여 채팅방에 메시지를 보냅니다.
    /// </summary>
    /// <param name="chatRoomHandle">채팅방 창 핸들</param>
    /// <param name="message">보낼 메시지 내용</param>
    /// <returns>전송 성공 여부</returns>
    public bool SendMessageToHandle(IntPtr chatRoomHandle, string message)
    {
        try
        {
            _logger.LogDebug("메시지 전송 시작 - 핸들: 0x{Handle:X}", chatRoomHandle);

            // 1단계: 창 강제 활성화 (포그라운드로 가져오기)
            ForceActivateWindow(chatRoomHandle);
            Thread.Sleep(500); // 창 활성화 안정화 대기

            // 2단계: 클립보드 기반 전송 시도 (가장 안정적)
            bool sent = TrySendViaClipboard(chatRoomHandle, message);

            if (!sent)
            {
                _logger.LogWarning("클립보드 전송 실패, Win32 API 직접 전송으로 전환합니다.");
                sent = TrySendViaWin32Direct(chatRoomHandle, message);
            }

            if (sent)
            {
                _logger.LogInformation("메시지 전송 성공: '{Message}'",
                    message.Length > 50 ? message[..50] + "..." : message);
            }
            else
            {
                _logger.LogError("모든 전송 방법이 실패했습니다.");
            }

            return sent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "메시지 전송 중 오류 발생");
            return false;
        }
    }

    /// <summary>
    /// 창을 강제로 포그라운드에 가져옵니다.
    /// AttachThreadInput을 사용하여 포그라운드 전환 제한을 우회합니다.
    /// </summary>
    private void ForceActivateWindow(IntPtr hWnd)
    {
        var foregroundWindow = GetForegroundWindow();
        var currentThreadId = GetCurrentThreadId();
        var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
        var targetThreadId = GetWindowThreadProcessId(hWnd, out _);

        // 현재 스레드를 포그라운드 창의 스레드에 연결
        if (currentThreadId != foregroundThreadId)
        {
            AttachThreadInput(currentThreadId, foregroundThreadId, true);
        }
        if (currentThreadId != targetThreadId)
        {
            AttachThreadInput(currentThreadId, targetThreadId, true);
        }

        // 최소화된 창 복원
        Win32Api.ShowWindow(hWnd, Win32Api.SW_RESTORE);
        Thread.Sleep(100);

        // 포그라운드로 가져오기
        SetForegroundWindow(hWnd);
        Thread.Sleep(100);

        // 스레드 입력 분리
        if (currentThreadId != foregroundThreadId)
        {
            AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }
        if (currentThreadId != targetThreadId)
        {
            AttachThreadInput(currentThreadId, targetThreadId, false);
        }

        _logger.LogDebug("창 강제 활성화 완료 - 핸들: 0x{Handle:X}", hWnd);
    }

    /// <summary>
    /// [1순위] 클립보드를 통한 메시지 전송
    /// 
    /// 동작 흐름:
    ///   1. 입력 필드에 포커스를 줍니다 (FlaUI 또는 마우스 클릭)
    ///   2. 클립보드에 메시지 텍스트를 복사합니다
    ///   3. Ctrl+V로 붙여넣기합니다 (keybd_event 사용)
    ///   4. Enter 키를 눌러 전송합니다
    /// </summary>
    private bool TrySendViaClipboard(IntPtr chatRoomHandle, string message)
    {
        try
        {
            // 입력 필드에 포커스 주기
            bool focused = FocusInputField(chatRoomHandle);
            if (!focused)
            {
                _logger.LogWarning("입력 필드 포커스 실패");
                return false;
            }

            Thread.Sleep(200);

            // 클립보드에 메시지 텍스트 복사
            SetClipboardText(message);
            Thread.Sleep(100);

            // Ctrl+V 붙여넣기 (keybd_event)
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero); // Ctrl 누르기
            Thread.Sleep(30);
            keybd_event(VK_V, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);       // V 누르기
            Thread.Sleep(30);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);         // V 떼기
            Thread.Sleep(30);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);   // Ctrl 떼기
            Thread.Sleep(300); // 붙여넣기 완료 대기

            // Enter 키 전송 (keybd_event)
            keybd_event(VK_RETURN, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(200);

            _logger.LogDebug("클립보드 기반 메시지 전송 완료");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "클립보드 전송 중 오류 발생");
            return false;
        }
    }

    /// <summary>
    /// 카카오톡 채팅방의 메시지 입력 필드에 포커스를 줍니다.
    /// FlaUI로 Edit 컨트롤을 찾아 Focus()를 호출합니다.
    /// </summary>
    private bool FocusInputField(IntPtr chatRoomHandle)
    {
        try
        {
            var window = _automation.FromHandle(chatRoomHandle);
            if (window == null)
            {
                _logger.LogDebug("FlaUI에서 창을 가져올 수 없습니다.");
                return false;
            }

            // Edit 컨트롤(입력 필드) 찾기
            var editElements = window.FindAllDescendants(
                new PropertyCondition(
                    _automation.PropertyLibrary.Element.ControlType,
                    ControlType.Edit));

            _logger.LogDebug("발견된 Edit 컨트롤 수: {Count}", editElements.Length);

            if (editElements.Length == 0)
            {
                // Edit를 못 찾으면 Document 타입도 시도 (RichEdit)
                editElements = window.FindAllDescendants(
                    new PropertyCondition(
                        _automation.PropertyLibrary.Element.ControlType,
                        ControlType.Document));
                _logger.LogDebug("Document 컨트롤 수: {Count}", editElements.Length);
            }

            AutomationElement? inputField = null;

            // 모든 Edit/Document 요소 중 입력 가능한 것 찾기
            foreach (var edit in editElements)
            {
                try
                {
                    var name = edit.Name ?? "";
                    var className = edit.ClassName ?? "";

                    _logger.LogDebug("Edit 요소 발견 - Name: '{Name}', Class: '{Class}', Enabled: {Enabled}",
                        name, className, edit.IsEnabled);

                    // 활성화된 Edit 컨트롤 = 입력 필드
                    if (edit.IsEnabled)
                    {
                        inputField = edit;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Edit 요소 검사 중 오류");
                }
            }

            if (inputField != null)
            {
                inputField.Focus();
                _logger.LogDebug("입력 필드 포커스 성공 (FlaUI)");
                return true;
            }

            // FlaUI로 못 찾으면 Win32 API로 Edit 컨트롤에 직접 포커스
            _logger.LogDebug("FlaUI로 입력 필드를 찾지 못함, Win32 API로 시도");
            return FocusInputFieldViaWin32(chatRoomHandle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "입력 필드 포커스 중 오류");
            return FocusInputFieldViaWin32(chatRoomHandle);
        }
    }

    /// <summary>
    /// Win32 API로 카카오톡 입력 필드에 포커스를 줍니다.
    /// </summary>
    private bool FocusInputFieldViaWin32(IntPtr chatRoomHandle)
    {
        try
        {
            var editHandles = FindEditControls(chatRoomHandle);

            if (editHandles.Count == 0)
            {
                _logger.LogDebug("Win32 API로 Edit 컨트롤을 찾지 못했습니다.");
                return false;
            }

            // 마지막 Edit 컨트롤이 메시지 입력 필드일 가능성이 높음
            var editHandle = editHandles.Last();
            Win32Api.SetForegroundWindow(editHandle);
            Win32Api.SendMessage(editHandle, 0x0007, IntPtr.Zero, IntPtr.Zero); // WM_SETFOCUS

            _logger.LogDebug("입력 필드 포커스 성공 (Win32) - Edit 핸들: 0x{Handle:X}", editHandle);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Win32 포커스 실패");
            return false;
        }
    }

    /// <summary>
    /// [2순위] Win32 API를 사용하여 메시지를 직접 전송합니다.
    /// Edit 컨트롤에 WM_SETTEXT 후 Enter 키 전송.
    /// </summary>
    private bool TrySendViaWin32Direct(IntPtr chatRoomHandle, string message)
    {
        try
        {
            var editHandles = FindEditControls(chatRoomHandle);

            if (editHandles.Count == 0)
            {
                _logger.LogDebug("Win32 API로 Edit 컨트롤을 찾지 못했습니다.");
                return false;
            }

            // 각 Edit 컨트롤에 대해 전송 시도 (마지막 것부터)
            for (int i = editHandles.Count - 1; i >= 0; i--)
            {
                var editHandle = editHandles[i];
                var className = Win32Api.GetWindowClassName(editHandle);

                _logger.LogDebug("Edit 컨트롤 시도 - 인덱스: {Index}, 클래스: {Class}, 핸들: 0x{Handle:X}",
                    i, className, editHandle);

                // 포커스 설정
                Win32Api.SendMessage(editHandle, 0x0007, IntPtr.Zero, IntPtr.Zero); // WM_SETFOCUS
                Thread.Sleep(100);

                // WM_SETTEXT로 텍스트 설정
                Win32Api.SendMessage(editHandle, Win32Api.WM_SETTEXT, IntPtr.Zero, message);
                Thread.Sleep(100);

                // 텍스트가 설정되었는지 확인
                int textLen = (int)Win32Api.SendMessage(editHandle, Win32Api.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
                if (textLen > 0)
                {
                    _logger.LogDebug("텍스트 설정 성공 (길이: {Length}), Enter 키 전송", textLen);

                    // Enter 키 전송
                    Win32Api.PostMessage(editHandle, Win32Api.WM_KEYDOWN,
                        (IntPtr)Win32Api.VK_RETURN, IntPtr.Zero);
                    Thread.Sleep(50);
                    Win32Api.PostMessage(editHandle, Win32Api.WM_KEYUP,
                        (IntPtr)Win32Api.VK_RETURN, IntPtr.Zero);

                    Thread.Sleep(200);
                    return true;
                }
            }

            _logger.LogDebug("Win32 직접 전송 실패 - 텍스트 설정 안 됨");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Win32 직접 전송 실패");
            return false;
        }
    }

    /// <summary>
    /// 채팅방 내 모든 Edit/RichEdit 컨트롤 핸들을 찾습니다.
    /// </summary>
    private List<IntPtr> FindEditControls(IntPtr chatRoomHandle)
    {
        var editHandles = new List<IntPtr>();

        Win32Api.EnumChildWindows(chatRoomHandle, (hWnd, lParam) =>
        {
            var className = Win32Api.GetWindowClassName(hWnd);

            // 카카오톡에서 사용할 수 있는 Edit 관련 클래스명 패턴
            if (className == "RICHEDIT50W" ||
                className == "RichEdit20W" ||
                className == "RichEdit50W" ||
                className == "Edit" ||
                className.Contains("RichEdit", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("Edit", StringComparison.OrdinalIgnoreCase))
            {
                editHandles.Add(hWnd);
                _logger.LogDebug("Edit 컨트롤 발견 - 클래스: {Class}, 핸들: 0x{Handle:X}", className, hWnd);
            }

            return true;
        }, IntPtr.Zero);

        return editHandles;
    }

    /// <summary>
    /// 클립보드에 텍스트를 설정합니다.
    /// STA 스레드에서 실행해야 합니다.
    /// </summary>
    private void SetClipboardText(string text)
    {
        Exception? clipboardError = null;
        var thread = new Thread(() =>
        {
            try
            {
                System.Windows.Forms.Clipboard.SetText(text, System.Windows.Forms.TextDataFormat.UnicodeText);
            }
            catch (Exception ex)
            {
                clipboardError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(3000); // 최대 3초 대기

        if (clipboardError != null)
        {
            _logger.LogError(clipboardError, "클립보드 텍스트 설정 실패");
            throw clipboardError;
        }

        _logger.LogDebug("클립보드에 텍스트 설정 완료 (길이: {Length})", text.Length);
    }

    public void Dispose()
    {
        _automation?.Dispose();
    }
}
