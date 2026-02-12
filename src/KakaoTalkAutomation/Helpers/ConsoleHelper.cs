namespace KakaoTalkAutomation.Helpers;

/// <summary>
/// 콘솔 출력 관련 유틸리티 메서드 모음
/// 메뉴 표시, 색상 출력 등을 담당합니다.
/// </summary>
public static class ConsoleHelper
{
    /// <summary>
    /// 프로그램 헤더(배너)를 출력합니다.
    /// </summary>
    public static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║     카카오톡 메시지 자동화 프로그램      ║");
        Console.WriteLine("║           v1.0.0                         ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    /// <summary>
    /// 메인 메뉴를 출력합니다.
    /// </summary>
    public static void PrintMenu()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("=== 메인 메뉴 ===");
        Console.ResetColor();
        Console.WriteLine("  1. 메시지 모니터링 시작/중지");
        Console.WriteLine("  2. 메시지 보내기");
        Console.WriteLine("  3. 저장된 메시지 조회");
        Console.WriteLine("  4. 채팅방 목록 보기");
        Console.WriteLine("  5. 카카오톡 연결 상태 확인");
        Console.WriteLine("  6. UI 구조 진단 (디버그)");
        Console.WriteLine("  0. 종료");
        Console.WriteLine();
        Console.Write("선택: ");
    }

    /// <summary>
    /// 성공 메시지를 녹색으로 출력합니다.
    /// </summary>
    /// <param name="message">출력할 메시지</param>
    public static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[✓] {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// 오류 메시지를 빨간색으로 출력합니다.
    /// </summary>
    /// <param name="message">출력할 메시지</param>
    public static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[✗] {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// 경고 메시지를 노란색으로 출력합니다.
    /// </summary>
    /// <param name="message">출력할 메시지</param>
    public static void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[!] {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// 안내 메시지를 파란색으로 출력합니다.
    /// </summary>
    /// <param name="message">출력할 메시지</param>
    public static void PrintInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"[i] {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// 구분선을 출력합니다.
    /// </summary>
    public static void PrintSeparator()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("──────────────────────────────────────────");
        Console.ResetColor();
    }

    /// <summary>
    /// 사용자에게 문자열 입력을 받습니다.
    /// </summary>
    /// <param name="prompt">입력 안내 메시지</param>
    /// <returns>사용자가 입력한 문자열</returns>
    public static string ReadInput(string prompt)
    {
        Console.Write($"{prompt}: ");
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// 사용자에게 확인(Y/N)을 받습니다.
    /// </summary>
    /// <param name="prompt">확인 안내 메시지</param>
    /// <returns>Y 선택 시 true</returns>
    public static bool Confirm(string prompt)
    {
        Console.Write($"{prompt} (Y/N): ");
        var input = Console.ReadLine()?.Trim().ToUpper();
        return input == "Y" || input == "YES" || input == "ㅛ";
    }
}
