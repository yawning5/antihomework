using System.Diagnostics;

namespace KakaoTalkAutomation;

/// <summary>
/// 카카오톡 채팅방 찾기
///
/// 원리: Windows에 떠 있는 모든 창을 순회하면서
///       카카오톡 프로세스에 속한 채팅방 창만 골라냅니다.
///
/// Spring으로 비유하면:
///   Process.GetProcessesByName("KakaoTalk") = DB에서 카카오톡 PID 조회
///   EnumWindows()                           = SELECT * FROM windows WHERE pid IN (...)
///   IsWindowVisible()                       = WHERE visible = true
///   GetTitle()                              = 각 row의 title 컬럼
/// </summary>
public static class ChatFinder
{
    /// <summary>
    /// 열려 있는 카카오톡 채팅방 목록을 반환합니다.
    /// </summary>
    public static List<(IntPtr Handle, string Name)> Find()
    {
        var rooms = new List<(IntPtr, string)>();

        // 1. 카카오톡 프로세스 ID 가져오기
        var pids = Process.GetProcessesByName("KakaoTalk")
                          .Select(p => (uint)p.Id)
                          .ToHashSet();
        if (pids.Count == 0) return rooms;

        // 2. 모든 창을 돌면서 카카오톡 + 보이는 창만 필터링
        Win32.EnumWindows((hWnd, _) =>
        {
            Win32.GetWindowThreadProcessId(hWnd, out uint pid);

            if (pids.Contains(pid) && Win32.IsWindowVisible(hWnd))
            {
                string title = Win32.GetTitle(hWnd);

                // "카카오톡" = 메인 창 → 건너뜀  /  빈 제목 → 건너뜀
                if (!string.IsNullOrEmpty(title) && title != "카카오톡")
                    rooms.Add((hWnd, title));
            }
            return true; // 계속 순회
        }, IntPtr.Zero);

        return rooms;
    }
}
