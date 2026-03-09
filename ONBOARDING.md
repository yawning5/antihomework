# 온보딩 문서 초안

## 1. 프로젝트 개요

이 프로젝트는 PC 카카오톡 메인 창을 제어해 채팅방을 검색하고 메시지를 보내는 Windows 콘솔 프로그램입니다.
핵심은 "특정 채팅방 창 핸들(HWND)을 직접 찾는 것"이 아니라, "카카오톡 메인 창의 키보드 포커스 흐름을 안정적으로 따라가는 것"입니다.

현재 기준 핵심 기능은 하나입니다.

- 채팅방 이름을 입력받아 메시지 자동 전송

## 2. 실행 환경

- 운영체제: Windows 10/11
- SDK: .NET 8
- 대상 앱: PC 카카오톡 로그인 상태
- 전제 조건 1: 카카오톡 메인 창이 화면에 떠 있어야 함
- 전제 조건 2: 채팅방 검색 후 Enter 입력 시 새 창으로 열려야 함

## 3. 프로젝트 구조

```text
src/KakaoTalkAutomation/
├── Program.cs
├── ChatFinder.cs
├── MessageSender.cs
├── Win32.cs
└── KakaoTalkAutomation.csproj
```

각 파일의 역할은 아래와 같습니다.

- `Program.cs`
  - 콘솔 메뉴 진입점
  - 채팅방 이름과 메시지 입력을 받아 전송 요청
- `ChatFinder.cs`
  - 카카오톡 메인 창 핸들 식별
- `MessageSender.cs`
  - `Ctrl+F`, `Ctrl+V`, `Enter`, `Ctrl+W`, `ESC` 시퀀스 수행
- `Win32.cs`
  - `user32.dll` 기반 Win32 API 선언
  - 포커스 전환, 키 입력 시뮬레이션, 클립보드 지원 보조

## 4. 실행 방법

저장소 루트 기준:

```bash
dotnet build src/KakaoTalkAutomation/KakaoTalkAutomation.csproj
dotnet run --project src/KakaoTalkAutomation
```

실행 시 표시되는 메뉴:

```text
=== 카카오톡 자동화 프로그램 ===

1. 메시지 보내기
0. 종료
```

## 5. 동작 방식 요약

`MessageSender.Send(roomName, message)`는 아래 순서로 동작합니다.

1. 카카오톡 프로세스를 찾고 메인 창을 식별
2. 메인 창을 앞으로 가져오고 잠시 대기
3. `Ctrl+F`로 검색창 포커스 이동
4. 채팅방 이름을 클립보드에 넣고 `Ctrl+V`
5. `Enter`로 채팅방 열기
6. 메시지를 클립보드에 넣고 `Ctrl+V`
7. `Enter`로 메시지 전송
8. `Ctrl+W`로 채팅창 닫기
9. `ESC`로 검색 상태 초기화

## 6. 신규 참여자가 먼저 이해해야 할 점

- 이 프로젝트는 키보드 매크로 기반 자동화입니다.
- 안정성의 핵심은 "정확한 핸들 탐색"보다 "포커스가 예상대로 움직이는지"입니다.
- UI 애니메이션이나 환경 속도에 따라 `Thread.Sleep` 값 튜닝이 필요할 수 있습니다.
- 사용 중 다른 앱으로 포커스가 이동하면 동작 보장이 없습니다.

## 7. 수정 시 우선 확인할 포인트

- 메인 창 탐색 실패 시 `ChatFinder.cs`의 메인 창 탐색 조건 확인
- 검색이 안 열리면 `Ctrl+F` 시점과 대기 시간 확인
- 채팅방은 열리는데 메시지 전송이 실패하면 팝업 전환 대기 시간과 붙여넣기 시점 확인
- 카카오톡 업데이트 이후 동작이 깨지면 단축키 흐름과 새 창 열기 동작부터 재검증
- 코드 변경 후 `README.md`, `ONBOARDING.md`, `STUDY_GUIDE.md` 동기화

## 8. 현재 문서 상태

이 문서는 온보딩용 초안입니다.
실제 운영 절차, 실패 시 복구 절차, 딜레이 튜닝 기준은 이후 보강이 필요합니다.
