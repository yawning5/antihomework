# 카카오톡 메시지 전송 자동화 프로그램

PC 카카오톡에서 열려 있는 채팅방을 찾아 메시지를 보내는 Windows 콘솔 프로그램입니다.

## 주요 기능

- 열려 있는 카카오톡 채팅방 목록 조회
- 선택한 채팅방으로 메시지 전송

## 기술 스택

| 항목 | 기술 |
|------|------|
| 언어 | C# |
| 프레임워크 | .NET 8 (`net8.0-windows`) |
| UI 제어 | Win32 P/Invoke |
| 클립보드 | Windows Forms Clipboard API |

## 사전 요구사항

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PC 카카오톡 설치 및 로그인
- 메시지를 보낼 채팅방이 팝업 창으로 열려 있어야 함

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

1. 채팅방 목록 보기
2. 메시지 보내기
0. 종료
```

### 1. 채팅방 목록 보기

- 카카오톡에서 채팅방을 별도 팝업 창으로 엽니다.
- 메뉴 `1`을 선택하면 현재 열린 채팅방 목록이 출력됩니다.

### 2. 메시지 보내기

- 카카오톡에서 대상 채팅방을 별도 팝업 창으로 엽니다.
- 메뉴 `2`를 선택합니다.
- 목록에서 채팅방 번호를 고릅니다.
- 보낼 메시지를 입력하면 전송을 시도합니다.

## 동작 방식

이 프로그램은 카카오톡 공식 API를 사용하지 않고 Windows UI 자동화 방식으로 동작합니다.

1. `Process.GetProcessesByName("KakaoTalk")`로 카카오톡 프로세스를 찾습니다.
2. `EnumWindows`로 모든 최상위 창을 순회하며 카카오톡 채팅방 창을 찾습니다.
3. `EnumChildWindows`로 채팅방 내부의 입력 컨트롤을 찾습니다.
4. 메시지를 클립보드에 복사한 뒤 `Ctrl+V`, `Enter` 키 입력을 시뮬레이션해 전송합니다.

## 제한 사항

- 팝업 창으로 열린 채팅방만 찾을 수 있습니다.
- 카카오톡 버전이 바뀌면 창 클래스명이나 동작 방식이 달라질 수 있습니다.
- 클립보드와 키 입력 시뮬레이션에 의존하므로 다른 작업 중이면 영향이 있을 수 있습니다.

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
