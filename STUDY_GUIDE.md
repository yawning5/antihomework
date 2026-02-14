# 📖 카카오톡 자동화 학습 가이드

> **대상**: 백엔드(Java/Spring) 개발자가 Windows 데스크톱 프로그램을 처음 다룰 때
>
> **목표**: Win32 API를 사용하여 카카오톡 PC 창을 제어하는 원리를 이해하기

---

## 1. 핵심 개념: "창 핸들(HWND)"이란?

백엔드에서 DB 테이블의 각 행(Row)에 Primary Key가 있듯이,
**Windows의 모든 UI 요소(창, 버튼, 입력 필드)에는 고유 식별자가 있습니다.**

이것이 바로 **HWND (Handle to Window)** = 창 핸들 입니다.

```
[웹 서비스]                        [Windows 데스크톱]
──────────────────────────────    ──────────────────────────────
HTTP 요청 → URL로 대상 지정       → HWND(창 핸들)로 대상 지정
DB 조회 → PK로 행 식별            → HWND로 창 식별
REST API → 엔드포인트 호출        → Win32 API 함수 호출
```

**핵심 포인트**: 카카오톡 채팅방도 하나의 "창"이고, 그 안의 입력 필드도 하나의 "창"입니다.
이 핸들만 알면 해당 UI 요소를 프로그래밍으로 제어할 수 있습니다.

---

## 2. Win32 API 란?

C#에서 Windows 운영체제의 기능을 직접 사용하는 방법입니다.
Java로 치면 **JNI(Java Native Interface)** 와 비슷한 개념입니다.

```csharp
// C#에서 Win32 API를 호출하는 방법 (P/Invoke)
// → Win32.cs에 모아둠
[DllImport("user32.dll")]
public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
```

| 항목 | Java 비유 | C# Win32 |
|:---|:---|:---|
| 네이티브 호출 | JNI | P/Invoke (DllImport) |
| 반환 타입 | long (포인터) | IntPtr (포인터) |
| DLL 파일 | .so / .dll | user32.dll |

**user32.dll** = Windows의 UI 관련 기능이 모여있는 핵심 라이브러리

---

## 3. 카카오톡 채팅방 찾기 — `ChatFinder.cs`

### 3-1. 모든 창을 순회하며 카카오톡 찾기

Windows에는 수백 개의 창이 떠 있습니다. 이 중 카카오톡 것만 골라야 합니다.

```
[전체 창 목록]
├── Chrome (Class: Chrome_WidgetWin_1)
├── 메모장 (Class: Notepad)
├── 카카오톡 메인 (Class: EVA_Window_Dblclk, Title: "카카오톡")     ← 건너뜀
├── 채팅방 "홍길동" (Class: EVA_Window_Dblclk, Title: "홍길동")     ← 이것!
├── 채팅방 "팀채팅" (Class: EVA_Window_Dblclk, Title: "팀채팅")     ← 이것!
└── ...
```

### 3-2. 실제 코드 (ChatFinder.Find)

```csharp
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
```

**백엔드 비유**: DB에서 `SELECT * FROM windows WHERE pid IN (...) AND visible = true AND title != '카카오톡'`

> 💡 **포인트**: 메인 창과 채팅방의 차이 = 제목이 `"카카오톡"`인지 아닌지

---

## 4. 채팅방 안의 입력 필드 찾기

채팅방 창 안에도 여러 "자식 창(Child Window)"이 있습니다:

```
[채팅방 "홍길동"] (HWND: 0x1234, Class: EVA_Window_Dblclk)
│
├── [메시지 목록 영역] (Class: EVA_VH_ListControl_Dblclk)  ← 메시지가 표시되는 곳
├── [입력 필드]        (Class: RichEdit50W)                  ← 여기에 타이핑!
├── [전송 버튼]        (Class: EVA_ChildWindow)              ← 전송 버튼
└── [기타 UI 요소들...]
```

### 입력 필드를 찾는 방법 (MessageSender.FindEditControl)

```csharp
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
```

> ⚠️ `return true`로 끝까지 순회하면서 **마지막** Edit 컨트롤을 저장합니다.
> 카카오톡에서 입력 필드가 마지막 Edit 자식이기 때문입니다.

**백엔드 비유**: 부모-자식 관계 = DB의 Foreign Key 관계
- 채팅방(부모) → 입력 필드(자식), 메시지 목록(자식), 버튼(자식)

---

## 5. 클래스명은 어떻게 알아내나요?

### 방법 1: Spy++ (Visual Studio 도구)
- Visual Studio → 도구 → Spy++ 실행
- 망원경 아이콘(🔍)을 카카오톡 창 위에 드래그하면 정보가 표시됨
- Class, Handle, Title 등을 확인 가능

### 방법 2: 코드로 직접 열거

```csharp
// 채팅방의 모든 자식 창을 출력
int index = 0;
Win32.EnumChildWindows(chatRoomHandle, (hWnd, _) =>
{
    var className = Win32.GetClassName(hWnd);
    var title = Win32.GetTitle(hWnd);
    Console.WriteLine($"[{index++}] Class={className}, Title={title}, Handle=0x{hWnd:X}");
    return true;
}, IntPtr.Zero);
```

출력 예시:
```
[0] Class=EVA_ChildWindow, Title=, Handle=0xA1234
[1] Class=EVA_VH_ListControl_Dblclk, Title=, Handle=0xB5678   ← 메시지 목록
[2] Class=RichEdit50W, Title=, Handle=0xC9ABC                  ← 입력 필드!
[3] Class=EVA_ChildWindow, Title=, Handle=0xDDEF0
```

> ⚠️ **주의**: 카카오톡 버전이 업데이트되면 클래스명이 바뀔 수 있습니다!

---

## 6. 메시지 보내기 — `MessageSender.cs`

### 핵심 흐름

```
1. 채팅방 창을 복원 + 앞으로 가져온다 (ShowWindow + SetForegroundWindow)
2. 입력 필드(Edit 컨트롤)를 찾아 포커스를 준다 (WM_SETFOCUS)
3. 클립보드에 보낼 텍스트를 넣는다 (Clipboard.SetText)
4. Ctrl+V를 시뮬레이션한다 (PressKeys)  ← 입력 필드에 텍스트 붙여넣기
5. Enter를 시뮬레이션한다 (PressKeys)    ← 전송!
```

### 왜 직접 입력하지 않고 클립보드를 쓰나요?

카카오톡의 입력 필드는 `RichEdit` 컨트롤이라 일반 텍스트 입력 방식(`WM_SETTEXT`)이
작동하지 않습니다. 그래서 "클립보드에 복사 → Ctrl+V 붙여넣기"가 가장 안정적입니다.

### 실제 코드 (MessageSender.Send)

```csharp
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

        // 3. 클립보드에 텍스트 복사 (STA 스레드 필요)
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
```

> 💡 **STA 스레드**: 클립보드는 COM 기반이라 STA(Single Thread Apartment) 스레드에서만
> 접근 가능합니다. `Win32.RunOnStaThread()`가 이를 처리합니다.

---

## 7. 메시지 읽기 — `MessageReader.cs`

### 왜 직접 읽을 수 없나요?

카카오톡의 메시지 목록은 `EVA_VH_ListControl_Dblclk`라는 **커스텀 컨트롤**입니다.
일반적인 ListView나 TextBox가 아니라서, `WM_GETTEXT` 같은 표준 API로 텍스트를
가져올 수 없습니다.

### 해결책: 클릭 → Ctrl+A → Ctrl+C (포커스 → 전체 선택 → 복사)

카카오톡은 Ctrl+A + Ctrl+C 시 클립보드에 메시지를 다음 형식으로 넣어줍니다:

```
[홍길동] [오후 3:45] 안녕하세요
[홍길동] [오후 3:46] 오늘 회의 몇 시예요?
[김철수] [오후 3:47] 3시입니다
```

### 실제 코드 흐름 (MessageReader.Read)

```csharp
public static List<ChatMsg> Read(IntPtr chatRoom)
{
    // 1. 창 활성화
    Win32.ShowWindow(chatRoom, 9);
    Win32.SetForegroundWindow(chatRoom);
    Thread.Sleep(300);

    // 2. 메시지 목록 영역 클릭 (포커스를 메시지 영역으로)
    ClickMessageArea(chatRoom);
    Thread.Sleep(300);

    // 3. Ctrl+A → Ctrl+C
    Win32.PressKeys(0x11, 0x41);   // Ctrl+A (전체 선택)
    Thread.Sleep(300);
    Win32.PressKeys(0x11, 0x43);   // Ctrl+C (복사)
    Thread.Sleep(500);

    // 4. 클립보드에서 텍스트 읽기 (STA 스레드)
    string? text = null;
    Win32.RunOnStaThread(() => text = Clipboard.GetText());

    // 5. 선택 해제 (다시 클릭)
    ClickMessageArea(chatRoom);

    // 6. 정규식 파싱
    return ParseKakaoText(text);
}
```

### 메시지 목록 클릭 (ClickMessageArea)

```csharp
private static void ClickMessageArea(IntPtr chatRoom)
{
    // EVA_VH_ListControl 클래스의 자식 컨트롤을 찾아서
    IntPtr listCtrl = IntPtr.Zero;
    Win32.EnumChildWindows(chatRoom, (hWnd, _) =>
    {
        if (Win32.GetClassName(hWnd).Contains("EVA_VH_ListControl"))
        { listCtrl = hWnd; return false; } // 찾으면 중단
        return true;
    }, IntPtr.Zero);

    // 해당 영역의 중앙을 마우스 클릭 (위치 저장 → 클릭 → 원래 위치 복원)
    var target = listCtrl != IntPtr.Zero ? listCtrl : chatRoom;
    Win32.GetWindowRect(target, out var r);
    Win32.GetCursorPos(out var saved);
    Win32.SetCursorPos((r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2);
    Win32.mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); // 왼쪽 버튼 누르기
    Win32.mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); // 왼쪽 버튼 떼기
    Win32.SetCursorPos(saved.X, saved.Y);              // 원래 위치 복원
}
```

### 메시지 파싱 (ParseKakaoText)

```csharp
private static List<ChatMsg> ParseKakaoText(string text)
{
    var pattern = @"^\[(.+?)\] \[(오전|오후) (\d{1,2}:\d{2})\] (.+)$";
    // 그룹1=보낸 사람, 그룹2=오전/오후, 그룹3=시간, 그룹4=내용
    // → ChatMsg { Sender, Content, Time }으로 변환
}
```

> ⚠️ **단점**: 이 방식은 채팅방 창을 잠깐 활성화해야 해서 화면을 빼앗습니다 (~1초).

---

## 8. 메시지 DB 저장 — `MessageDb.cs`

### EF Core + SQLite (JPA와 거의 동일)

```csharp
// ---- 엔티티 (JPA의 @Entity) ----
public class ChatMessage
{
    public int Id { get; set; }               // PK (자동 증가)
    public string ChatRoomName { get; set; }   // 채팅방 이름
    public string Sender { get; set; }         // 보낸 사람
    public string Content { get; set; }        // 내용
    public DateTime MessageTime { get; set; }  // 메시지 시간
    public DateTime CreatedAt { get; set; }    // 저장 시간
    public bool IsOutgoing { get; set; }       // 보낸/받은 메시지 구분
}

// ---- DbContext (JPA의 EntityManager) ----
public class AppDb : DbContext
{
    public DbSet<ChatMessage> Messages { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder opt)
        => opt.UseSqlite("Data Source=messages.db");
}

// ---- 서비스 클래스 ----
public class MessageDb
{
    private readonly AppDb _db = new();

    public void Initialize() => _db.Database.EnsureCreated(); // 테이블 자동 생성
    public void Save(string chatRoom, string sender, string content, DateTime time, bool isOutgoing)
        => _db.Messages.Add(new ChatMessage { ... }); _db.SaveChanges();
    public List<ChatMessage> GetRecent(int count = 20)
        => _db.Messages.OrderByDescending(m => m.CreatedAt).Take(count).ToList();
}
```

| C# (EF Core) | Java (Spring) |
|:---|:---|
| `AppDb : DbContext` | `EntityManager` |
| `ChatMessage` 클래스 | `@Entity` 클래스 |
| `_db.Messages.Add()` | `repository.save()` |
| `_db.SaveChanges()` | `@Transactional` 커밋 |
| `EnsureCreated()` | `hibernate.ddl-auto=update` |

---

## 9. 프로그램 진입점 — `Program.cs`

콘솔 기반의 CLI 메뉴 프로그램입니다.

```
=== 카카오톡 자동화 프로그램 ===

1. 채팅방 목록 보기     → ChatFinder.Find()
2. 메시지 보내기        → MessageSender.Send()
3. 메시지 읽기          → MessageReader.Read()
4. DB 저장된 메시지 보기 → MessageDb.GetRecent()
0. 종료
```

- 채팅방 선택은 `SelectChatRoom()` 헬퍼로 공통 처리
- 보낸 메시지(`isOutgoing: true`)와 읽은 메시지 모두 DB에 자동 저장

---

## 10. 프로젝트 구조 한눈에 보기

```
KakaoTalkAutomation/
│
├── Win32.cs           ← Win32 API 함수 모음 (DllImport + 편의 래퍼)
├── ChatFinder.cs      ← 카카오톡 채팅방 핸들 찾기
├── MessageReader.cs   ← 메시지 읽기 (클릭 → Ctrl+A → Ctrl+C → 파싱)
├── MessageSender.cs   ← 메시지 보내기 (클립보드 + Ctrl+V + Enter)
├── MessageDb.cs       ← DB 저장 (EF Core + SQLite)
├── Program.cs         ← 진입점 (CLI 메뉴)
└── KakaoTalkAutomation.csproj ← 프로젝트 설정
```

### 파일별 역할과 백엔드 비유

| 파일 | 역할 | Spring Boot 비유 |
|:---|:---|:---|
| `Win32.cs` | OS 네이티브 함수 선언 | JNI 래퍼 |
| `ChatFinder.cs` | 카카오톡 창 탐색 | Repository (조회) |
| `MessageReader.cs` | 메시지 읽기 | Service (비즈니스 로직) |
| `MessageSender.cs` | 메시지 보내기 | Service (비즈니스 로직) |
| `MessageDb.cs` | DB 저장/조회 | Repository + Entity + JPA Config |
| `Program.cs` | CLI 메뉴 + 메인 루프 | Controller + main() |

### 설계 특징

- **DI 없음**: 모든 핵심 클래스는 `static class`로 작성 → 의존성 주입 불필요
- **단순한 구조**: 폴더 분리 없이 7개 파일로 구성 → 진입 장벽 최소화
- **유일한 외부 패키지**: `Microsoft.EntityFrameworkCore.Sqlite` 하나뿐

---

## 11. Win32.cs 주요 함수 정리

### 원시 API (DllImport)

| 함수명 | 용도 | 백엔드 비유 |
|:---|:---|:---|
| `EnumWindows(callback)` | 모든 최상위 창 순회 | `findAll()` + 스트림 필터링 |
| `EnumChildWindows(parent, callback)` | 자식 창 순회 | 부모 ID로 자식 조회 |
| `IsWindowVisible(hwnd)` | 창이 보이는지 확인 | `WHERE visible = true` |
| `GetWindowThreadProcessId` | 창의 프로세스 ID 확인 | `WHERE pid = ?` |
| `SetForegroundWindow(hwnd)` | 창을 앞으로 가져오기 | — |
| `ShowWindow(hwnd, cmd)` | 창 표시/숨기기/복원 | — |
| `SendMessage(hwnd, msg)` | 창에 윈도우 메시지 전송 | REST API 호출 |
| `keybd_event(key)` | 키보드 입력 시뮬레이션 | — |
| `mouse_event(flags)` | 마우스 입력 시뮬레이션 | — |
| `SetCursorPos(x, y)` | 마우스 위치 이동 | — |
| `GetCursorPos(out pt)` | 현재 마우스 위치 | — |
| `GetWindowRect(hwnd, out rect)` | 창 위치/크기 | — |

### 편의 래퍼 메서드

| 메서드 | 용도 |
|:---|:---|
| `GetTitle(hwnd)` | 창 제목 가져오기 (GetWindowText 래퍼) |
| `GetClassName(hwnd)` | 창 클래스명 가져오기 |
| `PressKeys(params byte[])` | 키 조합 누르기 (누르고 → 떼기 자동화) |
| `RunOnStaThread(action)` | STA 스레드에서 실행 (클립보드 접근용) |

> 💡 `PressKeys`는 매개변수로 받은 키들을 순서대로 누른 뒤, 역순으로 뗍니다.
> 예: `PressKeys(0x11, 0x56)` = Ctrl 누르기 → V 누르기 → V 떼기 → Ctrl 떼기

---

## 12. 직접 만들어보기 순서 (추천)

처음부터 만들어본다면 이 순서를 추천합니다:

### Step 1: 프로젝트 생성 + Win32 API 선언

```csharp
// 콘솔 프로젝트 생성 후, Win32.cs에 DllImport 추가
[DllImport("user32.dll")] public static extern bool EnumWindows(...);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(...);
```

### Step 2: 채팅방 목록 출력 (ChatFinder)

```csharp
// EnumWindows로 카카오톡 프로세스의 보이는 창을 찾아 이름 출력
var rooms = ChatFinder.Find();
rooms.ForEach(r => Console.WriteLine(r.Name));
```

### Step 3: 메시지 보내기 (MessageSender)

```csharp
// FindEditControl → WM_SETFOCUS → Clipboard.SetText → PressKeys(Ctrl+V, Enter)
MessageSender.Send(chatRoomHandle, "안녕하세요!");
```

### Step 4: 메시지 읽기 (MessageReader)

```csharp
// ClickMessageArea → PressKeys(Ctrl+A, Ctrl+C) → Clipboard.GetText → 정규식 파싱
var msgs = MessageReader.Read(chatRoomHandle);
```

### Step 5: DB 저장 (MessageDb)

```csharp
// EF Core + SQLite로 메시지 저장 (JPA와 거의 동일)
var db = new MessageDb();
db.Initialize();
db.Save("홍길동", "나", "안녕!", DateTime.Now, true);
```

### Step 6: CLI 메뉴로 통합 (Program.cs)

```csharp
// while 루프 + switch로 메뉴 구성
// 채팅방 선택 → 기능 실행 → 결과 출력
```

---

## 13. 자주 겪는 문제와 해결법

### Q: 카카오톡 창을 찾지 못해요
**A**: 채팅방을 **"팝업 창"으로 열어야** 합니다. 카카오톡 메인 창 안에 있는 채팅은 감지가 안 됩니다.

### Q: 키보드 시뮬레이션이 작동하지 않아요
**A**: 프로그램을 **관리자 권한**으로 실행해야 합니다. `keybd_event`, `mouse_event`는 권한이 필요할 수 있습니다.

### Q: 클립보드 접근 시 에러가 나요
**A**: 클립보드는 STA 스레드에서만 접근 가능합니다. `Win32.RunOnStaThread()`를 사용하세요.

### Q: 메시지 전송 시 한글이 깨져요
**A**: 클립보드 + Ctrl+V 방식을 사용하면 한글 문제가 없습니다. `WM_CHAR`로 직접 입력하면 깨질 수 있습니다.

### Q: 클래스명이 문서와 다릅니다
**A**: 카카오톡 버전에 따라 클래스명이 변경될 수 있습니다. `EnumChildWindows`로 직접 확인하세요.

### Q: 메시지 읽기가 불안정해요
**A**: `Thread.Sleep()` 시간을 늘려보세요. 카카오톡이 반응하기 전에 다음 동작이 실행되면 실패합니다.
