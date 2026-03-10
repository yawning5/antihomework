using System.Runtime.InteropServices;
using System.Text;

namespace KakaoTalkAutomation;

/// <summary>
/// Win32 API 모음 — Windows OS 함수를 C#에서 호출하기 위한 선언들
///
/// [DllImport]는 Java의 JNI와 비슷한 개념입니다.
/// "이런 함수가 있구나" 정도만 알면 되고, 외울 필요 없습니다.
/// </summary>
public static class Win32
{
    // --- 콜백 타입 ---
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    public delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    // --- 창 찾기 ---
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumChildProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    static extern int _GetClassName(IntPtr hWnd, StringBuilder sb, int max);

    // --- 창 조작 ---
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);

    // --- 키보드 / 마우스 ---
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, int data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    // ---- 편의 메서드 (래퍼) ----

    /// <summary>창 제목 가져오기</summary>
    public static string GetTitle(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetWindowText(hWnd, sb, 256);
        return sb.ToString();
    }

    /// <summary>창 클래스명 가져오기</summary>
    public static string GetClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        _GetClassName(hWnd, sb, 256);
        return sb.ToString();
    }

    /// <summary>
    /// 키 조합을 누릅니다.
    /// 예: PressKeys(0x11, 0x56) = Ctrl+V
    /// </summary>
    public static void PressKeys(params byte[] keys)
    {
        foreach (var k in keys)
            keybd_event(k, 0, 0, UIntPtr.Zero);
        Thread.Sleep(30);
        for (int i = keys.Length - 1; i >= 0; i--)
            keybd_event(keys[i], 0, 0x0002 /*KEYUP*/, UIntPtr.Zero);
    }

    /// <summary>STA 스레드에서 실행 (클립보드 접근용)</summary>
    public static void RunOnStaThread(Action action)
    {
        var t = new Thread(() => action());
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join(3000);
    }

    // ---- 구조체 ----
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT  { public int Left, Top, Right, Bottom; }
}
