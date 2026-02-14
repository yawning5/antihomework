using System.Text.RegularExpressions;

namespace KakaoTalkAutomation;

/// <summary>
/// 카카오톡 메시지 읽기
///
/// 동작 흐름:
///   1. 채팅방 창을 앞으로 가져온다
///   2. 메시지 목록 영역을 클릭 (포커스를 메시지 영역으로)
///   3. Ctrl+A (전체 선택) → Ctrl+C (복사)
///   4. 클립보드 텍스트를 정규식으로 파싱
///
/// ※ 단점: 잠깐 화면을 뺏깁니다 (~1초)
/// </summary>
public static class MessageReader
{
    /// <summary>채팅방의 메시지를 읽어옵니다.</summary>
    public static List<ChatMsg> Read(IntPtr chatRoom)
    {
        try
        {
            // 1. 창 활성화
            Win32.ShowWindow(chatRoom, 9);
            Win32.SetForegroundWindow(chatRoom);
            Thread.Sleep(300);

            // 2. 메시지 목록 영역 클릭
            ClickMessageArea(chatRoom);
            Thread.Sleep(300);

            // 3. Ctrl+A → Ctrl+C
            Win32.PressKeys(0x11, 0x41);   // Ctrl+A (전체 선택)
            Thread.Sleep(300);
            Win32.PressKeys(0x11, 0x43);   // Ctrl+C (복사)
            Thread.Sleep(500);

            // 4. 클립보드에서 텍스트 읽기
            string? text = null;
            Win32.RunOnStaThread(() => text = Clipboard.GetText());

            // 5. 선택 해제
            ClickMessageArea(chatRoom);

            // 6. 파싱
            return string.IsNullOrEmpty(text)
                ? new List<ChatMsg>()
                : ParseKakaoText(text);
        }
        catch { return new List<ChatMsg>(); }
    }

    // ---- 내부 메서드 ----

    /// <summary>
    /// 메시지 목록 영역을 마우스 클릭합니다.
    ///
    /// 카카오톡에서 Ctrl+A는 메시지 목록에 포커스가 있어야 동작합니다.
    /// "EVA_VH_ListControl" 클래스의 컨트롤을 찾아 그 중앙을 클릭합니다.
    /// </summary>
    private static void ClickMessageArea(IntPtr chatRoom)
    {
        // 메시지 목록 컨트롤 찾기
        IntPtr listCtrl = IntPtr.Zero;
        Win32.EnumChildWindows(chatRoom, (hWnd, _) =>
        {
            if (Win32.GetClassName(hWnd).Contains("EVA_VH_ListControl"))
            { listCtrl = hWnd; return false; } // 찾으면 중단
            return true;
        }, IntPtr.Zero);

        var target = listCtrl != IntPtr.Zero ? listCtrl : chatRoom;
        if (!Win32.GetWindowRect(target, out var r)) return;

        // 마우스 위치 저장 → 클릭 → 원래 위치로 복원
        Win32.GetCursorPos(out var saved);
        int x = (r.Left + r.Right) / 2;
        int y = listCtrl != IntPtr.Zero
            ? (r.Top + r.Bottom) / 2
            : r.Top + (r.Bottom - r.Top) / 3;

        Win32.SetCursorPos(x, y);
        Win32.mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); // 왼쪽 버튼 누르기
        Thread.Sleep(30);
        Win32.mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); // 왼쪽 버튼 떼기
        Win32.SetCursorPos(saved.X, saved.Y);
    }

    /// <summary>
    /// 카카오톡 클립보드 텍스트를 파싱합니다.
    ///
    /// 카카오톡의 Ctrl+C 형식:
    ///   [이름] [오후 3:45] 메시지 내용
    ///   [이름] [오후 3:46] 여러 줄
    ///   메시지도 있습니다
    /// </summary>
    private static List<ChatMsg> ParseKakaoText(string text)
    {
        var msgs = new List<ChatMsg>();
        var pattern = @"^\[(.+?)\] \[(오전|오후) (\d{1,2}:\d{2})\] (.+)$";
        ChatMsg? current = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;

            // 날짜 구분선 건너뛰기 ("2024년 1월 1일 월요일")
            if (Regex.IsMatch(line, @"\d{4}년\s*\d{1,2}월\s*\d{1,2}일")) continue;

            var m = Regex.Match(line, pattern);
            if (m.Success)
            {
                if (current != null) msgs.Add(current);

                // 시간 파싱: "오후 3:45" → 15:45
                int hour = int.Parse(m.Groups[3].Value.Split(':')[0]);
                int min  = int.Parse(m.Groups[3].Value.Split(':')[1]);
                if (m.Groups[2].Value == "오후" && hour != 12) hour += 12;
                if (m.Groups[2].Value == "오전" && hour == 12) hour = 0;

                current = new ChatMsg
                {
                    Sender  = m.Groups[1].Value,
                    Content = m.Groups[4].Value,
                    Time    = DateTime.Today.AddHours(hour).AddMinutes(min)
                };
            }
            else if (current != null)
            {
                current.Content += "\n" + line; // 여러 줄 메시지
            }
        }
        if (current != null) msgs.Add(current);
        return msgs;
    }
}

/// <summary>읽어온 메시지 데이터</summary>
public class ChatMsg
{
    public string Sender { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Time { get; set; }
}
