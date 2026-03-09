# KakaoTalk Automation Client

Windows Forms 기반 클라이언트에서 PostgreSQL 연결 설정을 저장하고, `chat_out` 테이블을 1초 주기로 폴링해 카카오톡 메시지를 순차 발송하는 도구입니다.

## 현재 기능

- PostgreSQL 연결 정보 입력 및 저장
- PostgreSQL 연결 테스트
- `chat_out` 대기열 미리보기
- 1건 단위 순차 폴링/발송
- 발송 성공 시 `msg_id` 기준 즉시 삭제
- `Ctrl+F` 단독 테스트
- 수동 단건 메시지 전송 테스트

## 기술 스택

| 항목 | 기술 |
|------|------|
| UI | Windows Forms |
| 런타임 | .NET 8 (`net8.0-windows`) |
| DB 드라이버 | Npgsql (PostgreSQL) |
| 카카오톡 제어 | Win32 P/Invoke |
| 설정 저장 | JSON 파일 (`client-settings.json`) |

## 실행 전제

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PostgreSQL 접속 정보
- PC 카카오톡 설치 및 로그인
- 메시지 전송 시 카카오톡 메인 창이 화면에 떠 있어야 함
- 채팅방이 Enter 입력 시 새 창으로 열리도록 설정되어 있어야 함

## 실행

```bash
dotnet build src/KakaoTalkAutomation/KakaoTalkAutomation.csproj
dotnet run --project src/KakaoTalkAutomation
```

## 설정 파일

클라이언트 설정은 실행 파일 기준 동일 폴더의 `client-settings.json`에 저장됩니다.

저장 항목:

- Host
- Port
- Database
- Username
- Password
- Search Path
- SSL Require 여부
- Poll Interval (ms)
- Post Send Delay (ms)

## 화면 구성

- `PostgreSQL Settings`
  - 연결 정보 입력
  - `Save Settings`
  - `Test Connection`
- `Dispatch Worker`
  - 폴링 주기 설정
  - 메시지 간 지연 설정
  - `Start Polling`
  - `Stop Polling`
  - 상태/성공/실패 건수 표시
- `Manual Test`
  - `Test Ctrl+F`
  - `Send Manual Message`
  - 워커와 무관한 수동 진단용 입력
- `chat_out Preview`
  - 현재 대기열 상위 메시지 표시

## 프로젝트 구조

```text
src/KakaoTalkAutomation/
├── Program.cs
├── MainForm.cs
├── ClientSettings.cs
├── SettingsStore.cs
├── PostgresClient.cs
├── ChatFinder.cs
├── MessageSender.cs
├── Win32.cs
└── KakaoTalkAutomation.csproj
```

## 제한 사항

- 카카오톡 전송은 키보드 포커스 흐름에 의존합니다.
- `client-settings.json`에는 비밀번호가 평문으로 저장됩니다.
- 발송 성공 기준은 현재 MVP 수준에서 `전송 시퀀스 완료`입니다.
- 전송 성공 후 삭제 실패 시 중복 발송 위험이 있으므로 워커를 중지합니다.

## 현재 기준 버전

- 현재 문서 기준 산출물 버전: `output_v10`

## 라이선스

MIT License
