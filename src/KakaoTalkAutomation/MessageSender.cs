namespace KakaoTalkAutomation;

/// <summary>
/// 카카오톡 메시지 보내기
///
/// 동작 흐름:
///   1. 채팅방 창을 앞으로 가져온다
///   2. 입력 필드(Edit 컨트롤)를 찾아 포커스를 준다
///   3. 클립보드에 텍스트를 넣는다
///   4. Ctrl+V (붙여넣기) → Enter (전송)
///
/// ※ 카카오톡의 입력 필드가 RichEdit 컨트롤이라
///    WM_SETTEXT가 안 먹혀서, 클립보드+Ctrl+V 조합이 가장 안정적입니다.
/// </summary>
public static class MessageSender
{
    /// <summary>채팅방에 메시지를 보냅니다.</summary>
    public static bool Send(IntPtr chatRoom, string message)
    {
        try
        {
            // 1. 창 활성화
            Win32.ShowWindow(chatRoom, 9); // SW_RESTORE
            Win32.SetForegroundWindow(chatRoom);
            Thread.Sleep(500);

            // 2. 입력 필드 찾기 → 포커스
            var edit = FindEditControl(chatRoom);
            if (edit != IntPtr.Zero)
            {
                Win32.SendMessage(edit, 0x0007, IntPtr.Zero, IntPtr.Zero); // WM_SETFOCUS
                Thread.Sleep(200);
            }

            // 3. 클립보드에 텍스트 복사
            Win32.RunOnStaThread(() => Clipboard.SetText(message, TextDataFormat.UnicodeText));
            Thread.Sleep(100);

            // 4. Ctrl+V → Enter
            Win32.PressKeys(0x11, 0x56);   // Ctrl+V (붙여넣기)
            Thread.Sleep(300);
            Win32.PressKeys(0x0D);          // Enter (전송!)
            Thread.Sleep(200);

            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// 채팅방 안에서 입력 필드(Edit 컨트롤)를 찾습니다.
    ///
    /// EnumChildWindows로 자식 컨트롤을 순회하면서
    /// 클래스명에 "Edit"가 포함된 컨트롤을 찾습니다.
    /// </summary>
    private static IntPtr FindEditControl(IntPtr parent)
    {
        IntPtr found = IntPtr.Zero;
        Win32.EnumChildWindows(parent, (hWnd, _) =>
        {
            if (Win32.GetClassName(hWnd).Contains("Edit", StringComparison.OrdinalIgnoreCase))
                found = hWnd;
            return true; // 계속 순회 (마지막 Edit = 입력 필드)
        }, IntPtr.Zero);
        return found;
    }
}
