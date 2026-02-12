# 카카오톡 메시지 자동화 프로그램

PC 카카오톡의 메시지를 자동으로 수신·저장하고, 특정 채팅방에 메시지를 송신할 수 있는 Windows 데스크톱 프로그램입니다.

## 주요 기능

- **📩 메시지 수신 모니터링**: 카카오톡 채팅방의 새 메시지를 자동으로 감지하여 데이터베이스에 저장
- **📤 메시지 송신**: 프로그램을 통해 특정 채팅방에 메시지 전송
- **💾 메시지 저장/조회**: SQLite DB에 저장된 메시지를 채팅방별, 키워드별로 조회 및 검색
- **🔍 채팅방 탐색**: 현재 열려 있는 카카오톡 채팅방 목록 확인

## 기술 스택

| 항목 | 기술 |
|------|------|
| 언어 | C# |
| 프레임워크 | .NET 8 (Windows) |
| UI 자동화 | FlaUI (UIA3) + Win32 P/Invoke |
| 데이터베이스 | SQLite (Entity Framework Core) |
| 로깅 | Serilog |
| DI | Microsoft.Extensions.Hosting |

## 사전 요구사항

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 이상
- PC 카카오톡 설치 및 로그인

## 설치 및 실행

```bash
# 프로젝트 클론 또는 다운로드 후

# 빌드
cd antihomework2
dotnet build

# 실행
dotnet run --project src/KakaoTalkAutomation
```

## 사용법

프로그램을 실행하면 아래와 같은 콘솔 메뉴가 표시됩니다:

```
╔══════════════════════════════════════════╗
║     카카오톡 메시지 자동화 프로그램      ║
║           v1.0.0                         ║
╚══════════════════════════════════════════╝

=== 메인 메뉴 ===
  1. 메시지 모니터링 시작/중지
  2. 메시지 보내기
  3. 저장된 메시지 조회
  4. 채팅방 목록 보기
  5. 카카오톡 연결 상태 확인
  0. 종료
```

### 1. 메시지 모니터링

- 카카오톡에서 채팅방을 **팝업(별도 창)으로** 열어주세요
- 메뉴 1을 선택하면 열린 채팅방의 새 메시지를 자동으로 감지하여 DB에 저장합니다
- 모든 채팅방 또는 특정 채팅방만 모니터링할 수 있습니다

### 2. 메시지 보내기

- 카카오톡에서 대상 채팅방을 **팝업으로** 열어주세요
- 메뉴 2를 선택하고 채팅방과 메시지를 입력하면 자동으로 전송됩니다

### 3. 저장된 메시지 조회

- 최근 메시지, 채팅방별 메시지, 키워드 검색을 지원합니다
- DB 파일은 실행 파일 위치에 `kakaotalk_messages.db`로 저장됩니다

## 프로젝트 구조

```
src/KakaoTalkAutomation/
├── Program.cs                    # 진입점 및 CLI 메뉴
├── appsettings.json              # 설정 파일
├── Core/
│   ├── KakaoTalkFinder.cs        # 카카오톡 창 탐색 (Win32 API)
│   ├── MessageReader.cs          # 메시지 읽기 (FlaUI)
│   └── MessageSender.cs          # 메시지 보내기
├── Data/
│   ├── AppDbContext.cs            # EF Core 데이터베이스 컨텍스트
│   ├── Models/
│   │   └── ChatMessage.cs         # 메시지 엔티티
│   └── Repositories/
│       └── MessageRepository.cs   # 메시지 CRUD
├── Services/
│   ├── MessageMonitorService.cs   # 백그라운드 수신 모니터링
│   └── MessageService.cs          # 비즈니스 로직
└── Helpers/
    ├── Win32Api.cs                # Win32 API P/Invoke 선언
    └── ConsoleHelper.cs           # 콘솔 유틸리티
```

## 동작 원리

이 프로그램은 카카오톡 PC의 **공식 API가 아닌**, Windows UI 자동화를 사용합니다:

1. **Win32 API** (`FindWindow`, `EnumWindows` 등)로 카카오톡 창/채팅방 핸들을 찾습니다
2. **FlaUI** (UI Automation 3)로 채팅방의 메시지 목록 요소를 읽어옵니다
3. 새 메시지는 **SQLite** 데이터베이스에 자동 저장됩니다
4. 메시지 전송 시 채팅방의 입력 필드에 텍스트를 설정하고 Enter 키를 전송합니다

## 주의사항

> ⚠️ **카카오톡 업데이트 시** UI 구조가 변경되면 자동화 코드 수정이 필요할 수 있습니다.

> ⚠️ **채팅방은 반드시 팝업(별도 창)으로** 열어야 합니다. 메인 창 내부의 채팅은 탐지되지 않을 수 있습니다.

> ⚠️ **개인 용도로만** 사용해주세요. 카카오톡 이용약관을 준수해야 합니다.

## UI 요소 확인 방법

카카오톡 버전에 따라 UI 요소가 다를 수 있습니다. Windows SDK의 **Inspect.exe** 도구를 사용하여 UI 구조를 확인할 수 있습니다:

1. Windows SDK 설치 후 `Inspect.exe` 실행
2. 카카오톡 채팅방 위에 마우스를 올려 클래스명, 컨트롤 타입 등을 확인
3. 필요 시 `KakaoTalkFinder.cs`의 클래스명 배열을 수정

## 로그

실행 로그는 `logs/` 디렉터리에 날짜별로 저장됩니다:
```
logs/kakaotalk-20260212.log
```

## 라이선스

MIT License
