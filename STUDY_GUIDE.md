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
[DllImport("user32.dll")]
public static extern IntPtr FindWindow(string? className, string? windowName);
```

| 항목 | Java 비유 | C# Win32 |
|:---|:---|:---|
| 네이티브 호출 | JNI | P/Invoke (DllImport) |
| 반환 타입 | long (포인터) | IntPtr (포인터) |
| DLL 파일 | .so / .dll | user32.dll, kernel32.dll |

**user32.dll** = Windows의 UI 관련 기능이 모여있는 핵심 라이브러리

---

## 3. 카카오톡 창을 찾는 방법 (단계별)

### 3-1. 모든 창을 순회하며 카카오톡 찾기

Windows에는 수백 개의 창이 떠 있습니다. 이 중 카카오톡 것만 골라야 합니다.

```
[전체 창 목록]
├── Chrome (Class: Chrome_WidgetWin_1)
├── 메모장 (Class: Notepad)
├── 카카오톡 메인 (Class: EVA_Window_Dblclk, Title: "카카오톡")     ← 이것!
├── 채팅방 "홍길동" (Class: EVA_Window_Dblclk, Title: "홍길동")     ← 이것!
├── 채팅방 "팀채팅" (Class: EVA_Window_Dblclk, Title: "팀채팅")     ← 이것!
└── ...
```

**코드 흐름:**

```csharp
// 1단계: 카카오톡 프로세스의 ID를 가져온다
var processes = Process.GetProcessesByName("KakaoTalk");
var processId = processes[0].Id;  // 예: 12345

// 2단계: 모든 창을 순회하면서 카카오톡 프로세스의 창만 필터링
Win32Api.EnumWindows((hWnd, lParam) =>
{
    // 이 창의 프로세스 ID 확인
    Win32Api.GetWindowThreadProcessId(hWnd, out uint pid);

    if (pid == processId && Win32Api.IsWindowVisible(hWnd))
    {
        // 이 창은 카카오톡 프로세스의 보이는 창!
        string className = Win32Api.GetWindowClassName(hWnd); // "EVA_Window_Dblclk"
        string title = Win32Api.GetWindowTitle(hWnd);         // "홍길동"

        Console.WriteLine($"발견! 핸들={hWnd}, 이름={title}");
    }
    return true; // 계속 순회
}, IntPtr.Zero);
```

**백엔드 비유**: DB에서 `SELECT * FROM windows WHERE process_id = 12345 AND visible = true`

### 3-2. 메인 창 vs 채팅방 구분하기

```csharp
// 제목이 "카카오톡"이면 → 메인 창
// 제목이 다른 이름이면 → 채팅방 (그 이름 = 채팅방 이름)

if (title == "카카오톡")
    // 메인 창
else if (!string.IsNullOrEmpty(title))
    // 채팅방 → title이 채팅방 이름 ("홍길동", "팀채팅" 등)
```

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

### 입력 필드 핸들을 찾는 방법

```csharp
// 채팅방 창의 모든 자식 창을 순회
Win32Api.EnumChildWindows(chatRoomHandle, (childHwnd, lParam) =>
{
    string className = Win32Api.GetWindowClassName(childHwnd);

    // 클래스명에 "RichEdit" 또는 "Edit"가 포함되면 → 입력 필드!
    if (className.Contains("RichEdit") || className.Contains("Edit"))
    {
        Console.WriteLine($"입력 필드 발견! 핸들={childHwnd}");
        // 이 핸들을 저장해두면 나중에 텍스트를 넣을 수 있음
    }
    return true;
}, IntPtr.Zero);
```

**백엔드 비유**: 부모-자식 관계 = DB의 Foreign Key 관계
- 채팅방(부모) → 입력 필드(자식), 메시지 목록(자식), 버튼(자식)

---

## 5. 클래스명은 어떻게 알아내나요?

### 방법 1: Spy++ (Visual Studio 도구)
- Visual Studio → 도구 → Spy++ 실행
- 망원경 아이콘(🔍)을 카카오톡 창 위에 드래그하면 정보가 표시됨
- Class, Handle, Title 등을 확인 가능

### 방법 2: 코드로 직접 열거 (우리 프로그램의 "UI 진단" 메뉴)

```csharp
// 채팅방의 모든 자식 창을 출력
int index = 0;
Win32Api.EnumChildWindows(chatRoomHandle, (hWnd, lParam) =>
{
    var className = Win32Api.GetWindowClassName(hWnd);
    var title = Win32Api.GetWindowTitle(hWnd);
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

## 6. 메시지 보내기 원리

### 핵심 흐름

```
1. 채팅방 창을 앞으로 가져온다 (SetForegroundWindow)
2. 클립보드에 보낼 텍스트를 넣는다 (Clipboard.SetText)
3. Ctrl+V를 시뮬레이션한다 (keybd_event)  ← 입력 필드에 텍스트 붙여넣기
4. Enter를 시뮬레이션한다 (keybd_event)    ← 전송!
```

### 왜 직접 입력하지 않고 클립보드를 쓰나요?

카카오톡의 입력 필드는 `RichEdit` 컨트롤이라 일반 텍스트 입력 방식(`WM_SETTEXT`)이
작동하지 않습니다. 그래서 "클립보드에 복사 → Ctrl+V 붙여넣기"가 가장 안정적입니다.

```csharp
// 실제 코드 흐름
public bool SendMessage(IntPtr chatRoomHandle, string message)
{
    // 1. 창 활성화
    Win32Api.SetForegroundWindow(chatRoomHandle);
    Thread.Sleep(300);

    // 2. 클립보드에 텍스트 복사
    Clipboard.SetText(message);

    // 3. Ctrl+V (붙여넣기) 시뮬레이션
    keybd_event(VK_CONTROL, 0, 0, 0);         // Ctrl 누르기
    keybd_event(0x56 /* V */, 0, 0, 0);        // V 누르기
    keybd_event(0x56, 0, KEYEVENTF_KEYUP, 0);  // V 떼기
    keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0); // Ctrl 떼기
    Thread.Sleep(200);

    // 4. Enter 키 (전송)
    keybd_event(VK_RETURN, 0, 0, 0);
    keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, 0);

    return true;
}
```

---

## 7. 메시지 읽기 원리

### 왜 직접 읽을 수 없나요?

카카오톡의 메시지 목록은 `EVA_VH_ListControl_Dblclk`라는 **커스텀 컨트롤**입니다.
일반적인 ListView나 TextBox가 아니라서, `WM_GETTEXT` 같은 표준 API로 텍스트를
가져올 수 없습니다.

### 해결책: Ctrl+A → Ctrl+C (전체 선택 → 복사)

카카오톡은 Ctrl+A + Ctrl+C 시 클립보드에 메시지를 다음 형식으로 넣어줍니다:

```
[홍길동] [오후 3:45] 안녕하세요
[홍길동] [오후 3:46] 오늘 회의 몇 시예요?
[김철수] [오후 3:47] 3시입니다
```

이 텍스트를 **정규식으로 파싱**하면 보낸 사람, 시간, 내용을 추출할 수 있습니다:

```csharp
var pattern = @"^\[(.+?)\] \[(오전|오후) (\d{1,2}:\d{2})\] (.+)$";
// 그룹1=보낸 사람, 그룹2=오전/오후, 그룹3=시간, 그룹4=내용
```

### 클립보드 읽기 흐름

```
1. 채팅방 창을 활성화한다
2. 메시지 목록 영역을 마우스 클릭 (포커스 이동)
3. Ctrl+A (전체 선택) 시뮬레이션
4. Ctrl+C (복사) 시뮬레이션
5. 클립보드에서 텍스트 읽기
6. 정규식으로 파싱 → 메시지 목록 생성
```

> ⚠️ **단점**: 이 방식은 채팅방 창을 잠깐 활성화해야 해서 화면을 빼앗습니다.

---

## 8. 프로젝트 구조 한눈에 보기

```
KakaoTalkAutomation/
│
├── Helpers/
│   ├── Win32Api.cs         ← Win32 API 함수 선언 (DllImport 모음)
│   └── ConsoleHelper.cs    ← 콘솔 출력 유틸리티 (색깔, 메뉴)
│
├── Core/                   ← ★ 핵심 로직 (여기만 이해하면 됨)
│   ├── KakaoTalkFinder.cs  ← 카카오톡 창/채팅방 핸들 찾기
│   ├── MessageReader.cs    ← 메시지 읽기 (클립보드 방식)
│   └── MessageSender.cs    ← 메시지 보내기 (클립보드 + 키 입력)
│
├── Data/                   ← DB 관련 (EF Core, 익숙할 것)
│   ├── AppDbContext.cs     ← DbContext (JPA의 EntityManager)
│   ├── Models/ChatMessage.cs ← 엔티티 (JPA의 @Entity)
│   └── Repositories/MessageRepository.cs ← CRUD (Spring Data JPA Repository)
│
├── Services/               ← 서비스 계층 (Spring Service와 동일)
│   ├── MessageMonitorService.cs ← 백그라운드 폴링 (Spring @Scheduled)
│   └── MessageService.cs       ← 비즈니스 로직
│
└── Program.cs              ← 진입점 + DI 설정 + CLI (Spring Boot main)
```

### 백엔드와의 1:1 대응

| C# (.NET) | Java (Spring Boot) |
|:---|:---|
| `Program.cs` + `Host.CreateDefaultBuilder` | `@SpringBootApplication` + `main()` |
| `services.AddSingleton<T>()` | `@Component` / `@Service` |
| `services.AddScoped<T>()` | `@Scope("request")` |
| `services.AddDbContext<T>()` | `@EnableJpaRepositories` |
| `ILogger<T>` | `@Slf4j` / `LoggerFactory` |
| `IHostedService` | `@Scheduled` / `ApplicationRunner` |
| `appsettings.json` | `application.yml` |

---

## 9. 직접 만들어보기 순서 (추천)

처음부터 만들어본다면 이 순서를 추천합니다:

### Step 1: 창 찾기만 해보기

```csharp
// 콘솔 프로젝트를 만들고, user32.dll의 FindWindow만 호출해보세요
var handle = FindWindow("EVA_Window_Dblclk", "카카오톡");
Console.WriteLine($"카카오톡 핸들: {handle}");
```

### Step 2: 채팅방 목록 출력해보기

```csharp
// EnumWindows로 모든 카카오톡 창을 찾아 이름을 출력
```

### Step 3: 메시지 보내기

```csharp
// SetForegroundWindow → Clipboard.SetText → keybd_event(Ctrl+V, Enter)
```

### Step 4: 메시지 읽기

```csharp
// SetForegroundWindow → keybd_event(Ctrl+A, Ctrl+C) → Clipboard.GetText → 정규식 파싱
```

### Step 5: DB 저장

```csharp
// EF Core로 SQLite에 메시지 저장 (JPA와 거의 동일)
```

### Step 6: 백그라운드 모니터링

```csharp
// Timer로 주기적으로 메시지 확인 → 새 메시지만 DB에 저장
```

---

## 10. 주요 Win32 API 함수 정리

| 함수명 | 용도 | 백엔드 비유 |
|:---|:---|:---|
| `FindWindow(class, title)` | 클래스명/제목으로 창 찾기 | `SELECT * FROM windows WHERE class=? AND title=?` |
| `EnumWindows(callback)` | 모든 최상위 창 순회 | `findAll()` + 스트림 필터링 |
| `EnumChildWindows(parent, callback)` | 자식 창 순회 | 부모 ID로 자식 조회 |
| `GetClassName(hwnd)` | 컨트롤 타입 확인 | `instanceof` 체크 |
| `GetWindowText(hwnd)` | 창 제목/텍스트 읽기 | `getName()` |
| `SetForegroundWindow(hwnd)` | 창을 앞으로 가져오기 | (해당 없음) |
| `SendMessage(hwnd, msg)` | 창에 명령 보내기 | REST API 호출 |
| `keybd_event(key)` | 키보드 입력 시뮬레이션 | (해당 없음) |

---

## 11. 자주 겪는 문제와 해결법

### Q: 카카오톡 창을 찾지 못해요
**A**: 채팅방을 "팝업 창"으로 열어야 합니다. 카카오톡 메인 창 안에 있는 채팅은 감지가 안 됩니다.

### Q: `keybd_event`가 작동하지 않아요
**A**: 프로그램을 **관리자 권한**으로 실행해야 합니다. 키보드 시뮬레이션은 권한이 필요할 수 있습니다.

### Q: 메시지 전송 시 한글이 깨져요
**A**: 클립보드 + Ctrl+V 방식을 사용하면 한글 문제가 없습니다. `WM_CHAR`로 직접 입력하면 깨질 수 있습니다.

### Q: 클래스명이 문서와 다릅니다
**A**: 카카오톡 버전에 따라 클래스명이 변경될 수 있습니다. EnumChildWindows로 직접 확인하세요.
