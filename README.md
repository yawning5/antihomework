# 카카오톡 메시지 전송 자동화 프로그램

PC 카카오톡 메인 창을 기준으로 채팅방을 검색하고, 키보드 단축키 시퀀스로 메시지를 보내는 Windows 콘솔 프로그램입니다.

## 주요 기능

- 카카오톡 메인 창 포커스 제어
- `Ctrl+F` 기반 채팅방 검색
- 클립보드 + 키보드 매크로로 메시지 전송

## 기술 스택

| 항목 | 기술 |
|------|------|
| 언어 | C# |
| 프레임워크 | .NET 8 (`net8.0-windows`) |
| UI 제어 | Win32 P/Invoke |
| 입력 방식 | Clipboard + Keyboard Macro |

## 사전 요구사항

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PC 카카오톡 설치 및 로그인
- 카카오톡 메인 창이 화면에 떠 있어야 함
- 채팅방 Enter 입력 시 새 창으로 열리도록 카카오톡 설정이 맞아야 함

## 빌드 및 실행

저장소 루트 `C:\Coding\antihomework` 기준:

```bash
dotnet build src/KakaoTalkAutomation/KakaoTalkAutomation.csproj
dotnet run --project src/KakaoTalkAutomation
```

## 사용 방법

실행하면 아래 메뉴가 표시됩니다.

```text
=== 카카오톡 자동화 프로그램 ===

1. 메시지 보내기
0. 종료
```

메시지 전송 절차:

1. 카카오톡 메인 창을 화면에 띄웁니다.
2. 프로그램에서 대상 채팅방 이름을 입력합니다.
3. 보낼 메시지를 입력합니다.
4. 프로그램이 `Ctrl+F → 채팅방 이름 붙여넣기 → Enter → 메시지 붙여넣기 → Enter → Ctrl+W → ESC` 순서로 동작합니다.

## 동작 방식

이 프로그램은 카카오톡 공식 API를 사용하지 않고, 카카오톡 메인 창의 키보드 포커스 흐름에 의존합니다.

1. 카카오톡 메인 창을 찾고 앞으로 가져옵니다.
2. `Ctrl+F`로 검색 UI를 엽니다.
3. 채팅방 이름을 클립보드에서 붙여넣고 `Enter`를 보냅니다.
4. 새로 열린 채팅방 창에 메시지를 붙여넣고 `Enter`를 보냅니다.
5. `Ctrl+W`로 채팅창을 닫고 `ESC`로 검색 상태를 정리합니다.

## 제한 사항

- 사용자가 마우스를 움직이거나 다른 창을 클릭하면 오작동할 수 있습니다.
- 카카오톡 UI 구조나 단축키 동작이 바뀌면 실패할 수 있습니다.
- 채팅방이 새 창이 아니라 메인 창 내부에 열리도록 설정된 경우 전송 흐름이 깨질 수 있습니다.
- `Thread.Sleep` 기반 대기 시간이 환경에 따라 부족하거나 과할 수 있습니다.

## 프로젝트 구조

```text
src/KakaoTalkAutomation/
├── Program.cs
├── ChatFinder.cs
├── MessageSender.cs
├── Win32.cs
└── KakaoTalkAutomation.csproj
```

## 라이선스

MIT License
