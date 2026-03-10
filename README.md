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
| 발송 로그 | JSONL 파일 (`dispatch-log.jsonl`) |

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

## 발송 로그 파일

발송 로그는 설정 파일과 분리된 `dispatch-log.jsonl`에 append 방식으로 저장됩니다.

건별 기록 항목:

- `msg_id`
- `room_name`
- `polled_at`
- `sequence_completed_at`
- `duration_ms`
- `duration_sec`
- `result`
- `detail`

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
- 발송 시간 로그는 `dispatch-log.jsonl`에 누적 저장됩니다.

## 현재 기준 버전

- 현재 문서 기준 산출물 버전: `output_v10`

## 라이선스

MIT License

## 워크플로우 (동작 방식)

카카오톡 자동화 클라이언트의 메시지 발송 메커니즘은 다음과 같은 순서로 1건씩 **순차적(Sequential)**으로 동작합니다.

1. **상태 폴링 (DB 조회)**
   - 지정된 폴링 주기(`Poll Interval`)마다 PostgreSQL의 `chat_out` 테이블을 조회합니다.
   - 이때 `ORDER BY msg_id ASC LIMIT 1` 쿼리를 사용하여, **PK(`msg_id`) 기준 가장 오래된(먼저 등록된) 메시지 1건**을 가져옵니다.
2. **메시지 전송 (UI 자동화)**
   - 가져온 1건의 메시지에 대해 카카오톡 UI 제어(Win32 API)를 시작합니다.
   - **주의:** 전송 매크로가 도는 동안에는 **다음 메시지를 폴링하지 않고 대기**합니다. 즉, 현재 메시지의 발송 시퀀스(창 찾기 -> 열기 -> 텍스트 입력 -> 전송 -> 창 닫기)가 완전히 끝날 때까지 블로킹됩니다.
3. **결과 확인 및 DB 삭제**
   - 전송 시퀀스가 정상 완료되었다고 판정되면(현재 MVP 기준), DB에서 해당 `msg_id`의 레코드를 즉시 `DELETE` 합니다.
   - 삭제에 실패할 경우 중복 발송을 방지하기 위해 워커가 즉시 에러를 발생시키고 중단됩니다.
4. **발송 후 대기 (Post Send Delay)**
   - 전송 및 삭제 처리가 완료된 후, 설정된 `Post Send Delay (ms)`만큼 대기합니다.
   - 대기가 끝나면 다시 1번(폴링) 단계로 돌아가 다음 메시지를 찾습니다.

결론적으로 멀티스레드로 동시에 여러 건을 쏘는 것이 아니라, **DB에서 PK 기준으로 한 건씩 꺼내와 발송 매크로를 돌리고, 발송이 완전히 끝나면 다음 건을 꺼내오는 안전한 단방향 큐(Queue) 방식**으로 동작합니다.
