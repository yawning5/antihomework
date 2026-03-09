# KakaoTalk Automation Client

Windows Forms 기반 클라이언트에서 PostgreSQL 연결 설정을 저장하고, 기본 조회를 실행하고, 카카오톡 메시지 전송까지 수행하는 도구입니다.

## 현재 기능

- PostgreSQL 연결 정보 입력 및 저장
- PostgreSQL 연결 테스트
- SQL 직접 실행과 결과 그리드 확인
- 카카오톡 채팅방 이름 + 메시지 수동 전송

## 기술 스택

| 항목 | 기술 |
|------|------|
| UI | Windows Forms |
| 런타임 | .NET 8 (`net8.0-windows`) |
| DB 드라이버 | Npgsql |
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
- 기본 SQL

## 화면 구성

- `PostgreSQL Settings`
  - 연결 정보 입력
  - `Save Settings`
  - `Test Connection`
- `DB Query`
  - SQL 입력
  - `Run Query`
- `Query Result`
  - 조회 결과 표시
- `KakaoTalk Send`
  - 채팅방 이름 입력
  - 메시지 입력
  - `Send Message`

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

- 현재 DB 조회는 사용자가 직접 입력한 SQL을 실행하는 기본 도구 수준입니다.
- 카카오톡 전송은 여전히 키보드 포커스 흐름에 의존합니다.
- `client-settings.json`에는 비밀번호가 평문으로 저장됩니다.

## 라이선스

MIT License
