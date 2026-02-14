namespace KakaoTalkAutomation;

/// <summary>
/// 카카오톡 자동화 프로그램 진입점
///
/// 메뉴:
///   1. 채팅방 목록 보기
///   2. 메시지 보내기
///   3. 메시지 읽기
///   0. 종료
/// </summary>
class Program
{
    static readonly MessageDb _db = new();

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        _db.Initialize();
        Console.WriteLine("=== 카카오톡 자동화 프로그램 ===\n");

        while (true)
        {
            Console.WriteLine("1. 채팅방 목록 보기");
            Console.WriteLine("2. 메시지 보내기");
            Console.WriteLine("3. 메시지 읽기");
            Console.WriteLine("4. DB 저장된 메시지 보기");
            Console.WriteLine("0. 종료");
            Console.Write("\n선택: ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1": ShowChatRooms(); break;
                case "2": HandleSend(); break;
                case "3": HandleRead(); break;
                case "4": ShowSavedMessages(); break;
                case "0": return;
            }
            Console.WriteLine();
        }
    }

    // ---- 1. 채팅방 목록 ----
    static void ShowChatRooms()
    {
        var rooms = ChatFinder.Find();
        if (rooms.Count == 0)
        {
            Console.WriteLine("  ❌ 열린 채팅방이 없습니다. 채팅방을 팝업 창으로 열어주세요.");
            return;
        }
        for (int i = 0; i < rooms.Count; i++)
            Console.WriteLine($"  {i + 1}. {rooms[i].Name}");
    }

    // ---- 2. 메시지 보내기 ----
    static void HandleSend()
    {
        var room = SelectChatRoom();
        if (room == null) return;

        Console.Write("  보낼 메시지: ");
        var msg = Console.ReadLine();
        if (string.IsNullOrEmpty(msg)) return;

        var ok = MessageSender.Send(room.Value.Handle, msg);
        Console.WriteLine(ok ? "  ✅ 전송 성공!" : "  ❌ 전송 실패");

        if (ok)
            _db.Save(room.Value.Name, "나", msg, DateTime.Now, isOutgoing: true);
    }

    // ---- 3. 메시지 읽기 ----
    static void HandleRead()
    {
        var room = SelectChatRoom();
        if (room == null) return;

        Console.WriteLine("  메시지 읽는 중... (잠깐 화면을 뺏습니다)");
        var msgs = MessageReader.Read(room.Value.Handle);

        if (msgs.Count == 0)
        {
            Console.WriteLine("  메시지를 읽지 못했습니다.");
            return;
        }

        Console.WriteLine($"\n  === {msgs.Count}개 메시지 ===");
        foreach (var m in msgs)
        {
            Console.WriteLine($"  [{m.Time:HH:mm}] {m.Sender}: {m.Content}");
            _db.Save(room.Value.Name, m.Sender, m.Content, m.Time, isOutgoing: false);
        }
        Console.WriteLine($"  ✅ DB에 {msgs.Count}개 저장 완료");
    }

    // ---- 4. 저장된 메시지 ----
    static void ShowSavedMessages()
    {
        var msgs = _db.GetRecent();
        if (msgs.Count == 0) { Console.WriteLine("  저장된 메시지가 없습니다."); return; }

        foreach (var m in msgs)
        {
            var dir = m.IsOutgoing ? "→" : "←";
            Console.WriteLine($"  [{m.MessageTime:MM-dd HH:mm}] {dir} {m.Sender}: {m.Content}");
        }
    }

    // ---- 채팅방 선택 헬퍼 ----
    static (IntPtr Handle, string Name)? SelectChatRoom()
    {
        var rooms = ChatFinder.Find();
        if (rooms.Count == 0)
        {
            Console.WriteLine("  ❌ 열린 채팅방이 없습니다.");
            return null;
        }

        for (int i = 0; i < rooms.Count; i++)
            Console.WriteLine($"  {i + 1}. {rooms[i].Name}");

        Console.Write("  번호 선택: ");
        if (!int.TryParse(Console.ReadLine(), out int n) || n < 1 || n > rooms.Count)
        {
            Console.WriteLine("  잘못된 번호입니다.");
            return null;
        }
        return rooms[n - 1];
    }
}
