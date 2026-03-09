namespace KakaoTalkAutomation;

/// <summary>
/// 카카오톡 자동화 프로그램 진입점
///
///   1. 메시지 보내기
///   0. 종료
/// </summary>
class Program
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== 카카오톡 자동화 프로그램 ===\n");
        Console.WriteLine("카카오톡 메인 창을 먼저 화면에 띄워두고 사용하세요.");
        Console.WriteLine("채팅방은 Enter 입력 시 새 창으로 열리도록 설정되어 있어야 합니다.\n");

        while (true)
        {
            Console.WriteLine("1. 메시지 보내기");
            Console.WriteLine("0. 종료");
            Console.Write("\n선택: ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1": HandleSend(); break;
                case "0": return;
            }

            Console.WriteLine();
        }
    }

    static void HandleSend()
    {
        Console.Write("  대상 채팅방 이름: ");
        var roomName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(roomName)) return;

        Console.Write("  보낼 메시지: ");
        var msg = Console.ReadLine();
        if (string.IsNullOrEmpty(msg)) return;

        var ok = MessageSender.Send(roomName, msg);
        Console.WriteLine(ok
            ? "  ✅ 전송 성공!"
            : "  ❌ 전송 실패 - 카카오톡 메인 창 상태와 새 창 열기 설정을 확인하세요.");
    }
}
