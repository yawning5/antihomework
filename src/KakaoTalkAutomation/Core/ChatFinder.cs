using System.Diagnostics;

namespace KakaoTalkAutomation;

/// <summary>
/// 카카오톡 메인 창 찾기
///
/// 예전처럼 열린 채팅방 목록을 수집하는 역할은 제거하고,
/// 현재는 메시지 전송 시작점이 되는 메인 카카오톡 창만 식별합니다.
/// </summary>
public static class ChatFinder
{
    /// <summary>카카오톡 메인 창 핸들을 반환합니다.</summary>
    public static IntPtr FindMainWindow()
    {
        var pids = Process.GetProcessesByName("KakaoTalk")
            .Select(p => (uint)p.Id)
            .ToHashSet();
        if (pids.Count == 0) return IntPtr.Zero;

        IntPtr found = IntPtr.Zero;
        Win32.EnumWindows((hWnd, _) =>
        {
            Win32.GetWindowThreadProcessId(hWnd, out uint pid);
            var title = Win32.GetTitle(hWnd);

            if (pids.Contains(pid) &&
                Win32.IsWindowVisible(hWnd) &&
                string.Equals(title, "카카오톡", StringComparison.Ordinal))
            {
                found = hWnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }
}
