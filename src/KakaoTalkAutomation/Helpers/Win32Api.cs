using System.Runtime.InteropServices;
using System.Text;

namespace KakaoTalkAutomation.Helpers;

/// <summary>
/// Win32 API P/Invoke 선언 모음
/// 카카오톡 PC 창을 찾고 조작하기 위한 Windows API 함수들을 정의합니다.
/// </summary>
public static class Win32Api
{
    // ========================================
    // 델리게이트 정의
    // ========================================

    /// <summary>
    /// EnumWindows 콜백 함수 델리게이트
    /// </summary>
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// EnumChildWindows 콜백 함수 델리게이트
    /// </summary>
    public delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    // ========================================
    // 창 탐색 관련 API
    // ========================================

    /// <summary>
    /// 클래스명과 창 이름으로 최상위 창 핸들을 찾습니다.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    /// <summary>
    /// 부모 창 내에서 자식 창을 찾습니다.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    /// <summary>
    /// 모든 최상위 창을 열거합니다.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    /// <summary>
    /// 부모 창의 모든 자식 창을 열거합니다.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    // ========================================
    // 창 정보 관련 API
    // ========================================

    /// <summary>
    /// 창의 제목(텍스트)을 가져옵니다.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    /// <summary>
    /// 창 제목의 길이를 반환합니다.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    /// <summary>
    /// 창의 클래스명을 가져옵니다.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    /// <summary>
    /// 창이 보이는지(표시 상태) 확인합니다.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    /// <summary>
    /// 창을 생성한 프로세스 ID를 가져옵니다.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // ========================================
    // 창 조작 관련 API
    // ========================================

    /// <summary>
    /// 지정된 창을 포그라운드(최상위)로 가져옵니다.
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// 창의 표시 상태를 변경합니다 (최소화, 복원 등).
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>
    /// 창에 메시지를 보냅니다 (동기).
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// 창에 메시지를 보냅니다 (문자열 파라미터 버전).
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

    /// <summary>
    /// 창에 메시지를 보냅니다 (비동기).
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// 현재 포그라운드 창의 핸들을 반환합니다.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    // ========================================
    // Windows 메시지 상수
    // ========================================

    /// <summary>텍스트 설정 메시지</summary>
    public const uint WM_SETTEXT = 0x000C;

    /// <summary>텍스트 가져오기 메시지</summary>
    public const uint WM_GETTEXT = 0x000D;

    /// <summary>텍스트 길이 가져오기 메시지</summary>
    public const uint WM_GETTEXTLENGTH = 0x000E;

    /// <summary>키 눌림 메시지</summary>
    public const uint WM_KEYDOWN = 0x0100;

    /// <summary>키 올림 메시지</summary>
    public const uint WM_KEYUP = 0x0101;

    /// <summary>문자 입력 메시지</summary>
    public const uint WM_CHAR = 0x0102;

    /// <summary>Enter 키 가상 키코드</summary>
    public const int VK_RETURN = 0x0D;

    /// <summary>Ctrl 키 가상 키코드</summary>
    public const int VK_CONTROL = 0x11;

    // ========================================
    // ShowWindow 명령 상수
    // ========================================

    /// <summary>창 숨기기</summary>
    public const int SW_HIDE = 0;

    /// <summary>창 보통 상태로 표시</summary>
    public const int SW_SHOWNORMAL = 1;

    /// <summary>창 최소화</summary>
    public const int SW_MINIMIZE = 6;

    /// <summary>창 복원</summary>
    public const int SW_RESTORE = 9;

    // ========================================
    // 유틸리티 메서드
    // ========================================

    /// <summary>
    /// 창 핸들로부터 창 제목 텍스트를 가져옵니다.
    /// </summary>
    /// <param name="hWnd">창 핸들</param>
    /// <returns>창 제목 문자열</returns>
    public static string GetWindowTitle(IntPtr hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length == 0) return string.Empty;

        var sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// 창 핸들로부터 클래스명을 가져옵니다.
    /// </summary>
    /// <param name="hWnd">창 핸들</param>
    /// <returns>클래스명 문자열</returns>
    public static string GetWindowClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
