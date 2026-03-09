namespace KakaoTalkAutomation;

/// <summary>
/// 카카오톡 메시지 보내기
///
/// 동작 흐름:
///   1. 카카오톡 메인 창을 앞으로 가져온다
///   2. Ctrl+F로 채팅방 검색창을 연다
///   3. 채팅방 이름을 붙여넣고 Enter로 채팅방을 연다
///   4. 메시지를 붙여넣고 Enter로 전송한다
///   5. Ctrl+W와 ESC로 채팅창을 닫고 검색 상태를 정리한다
///
/// ※ 이 방식은 카카오톡의 키보드 포커스 흐름에 의존합니다.
///    사용자가 다른 창을 클릭하거나 포커스를 뺏으면 즉시 실패할 수 있습니다.
/// </summary>
public static class MessageSender
{
    private const int WindowReadyDelayMs = 500;
    private const int SearchReadyDelayMs = 250;
    private const int RoomSearchDelayMs = 1000;
    private const int PopupOpenDelayMs = 700;
    private const int PasteDelayMs = 120;
    private const int KeySequenceDelayMs = 300;

    /// <summary>채팅방에 메시지를 보냅니다.</summary>
    public static bool Send(string roomName, string message)
    {
        try
        {
            var mainWindow = ChatFinder.FindMainWindow();
            if (mainWindow == IntPtr.Zero) return false;

            Win32.ShowWindow(mainWindow, 9); // SW_RESTORE
            Win32.SetForegroundWindow(mainWindow);
            Thread.Sleep(WindowReadyDelayMs);

            Win32.PressKeys(0x11, 0x46); // Ctrl+F
            Thread.Sleep(SearchReadyDelayMs);

            PasteText(roomName);
            Thread.Sleep(RoomSearchDelayMs);
            Win32.PressKeys(0x0D); // Enter
            Thread.Sleep(PopupOpenDelayMs);

            PasteText(message);
            Thread.Sleep(PasteDelayMs);
            Win32.PressKeys(0x0D); // Enter
            Thread.Sleep(KeySequenceDelayMs);

            Win32.PressKeys(0x11, 0x57); // Ctrl+W
            Thread.Sleep(KeySequenceDelayMs);
            Win32.PressKeys(0x1B); // ESC
            Thread.Sleep(KeySequenceDelayMs);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void PasteText(string text)
    {
        Win32.RunOnStaThread(() => Clipboard.SetText(text, TextDataFormat.UnicodeText));
        Win32.PressKeys(0x11, 0x56); // Ctrl+V
    }
}
