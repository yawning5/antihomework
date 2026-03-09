# 온보딩 문서

## 1. 프로젝트 개요

이 프로젝트는 두 축으로 구성됩니다.

- PostgreSQL `chat_out` 대기열을 폴링하는 WinForms 클라이언트
- 카카오톡 메인 창을 제어해 메시지를 전송하는 자동화 모듈

이제 진입점은 CLI가 아니라 Windows 클라이언트입니다.

## 2. 핵심 파일

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

- `Program.cs`
  - WinForms 앱 시작
- `MainForm.cs`
  - 메인 UI
  - DB 설정/연결 테스트/워커 시작/미리보기/수동 진단 제공
- `ClientSettings.cs`
  - 설정 모델
- `SettingsStore.cs`
  - `client-settings.json` 읽기/쓰기
- `PostgresClient.cs`
  - Npgsql 기반 PostgreSQL 연결과 조회 실행
- `ChatOutRepository.cs`
  - `chat_out` 1건 조회 / 미리보기 조회 / 삭제
- `MessageDispatchService.cs`
  - 조회 -> 전송 -> 성공 판정 -> 삭제 orchestration
- `ISendConfirmationPolicy.cs`
  - 발송 성공 판정 추상화
- `SequenceCompletedConfirmationPolicy.cs`
  - 현재 MVP 성공 판정 구현
- `ChatFinder.cs`
  - 카카오톡 메인 창 식별
- `MessageSender.cs`
  - 채팅방 검색과 메시지 전송 시퀀스 수행
- `Win32.cs`
  - Win32 API 래퍼

## 3. 실행 흐름

### DB 쪽

1. 사용자가 PostgreSQL 연결 정보를 입력
2. `Save Settings`로 로컬 설정 파일 저장
3. `Test Connection`으로 연결 확인
4. `Start Polling`으로 워커 시작
5. 워커가 `chat_out`에서 `msg_id ASC LIMIT 1`로 1건 조회
6. 발송 성공 시 해당 `msg_id`를 즉시 삭제

### 카카오톡 쪽

1. 워커가 `room_nm`, `msg`를 읽음
2. 메인 카카오톡 창을 찾음
3. `Ctrl+F` 검색, 채팅방 열기, 메시지 전송 시퀀스 수행
4. 현재 MVP에서는 시퀀스 완료를 성공으로 간주

### 수동 진단 쪽

1. `Manual Test` 영역에서 `Test Ctrl+F`로 검색 단축키만 확인
2. 필요하면 `room name`, `message`를 직접 넣고 `Send Manual Message` 실행
3. 워커 문제인지, 카카오톡 입력 문제인지 분리 진단

## 4. 설정 파일

설정은 실행 폴더의 `client-settings.json`에 저장됩니다.

주의:

- PostgreSQL 비밀번호가 평문으로 저장됩니다.
- 배포 전 암호화나 Windows 자격 증명 저장소 사용 여부를 검토할 필요가 있습니다.

## 5. 다음 확장 포인트

- 발송 성공 확인 강화
- DB 작업 큐 상태 컬럼 도입
- 메시지 발송 이력 저장
- 설정 암호화
- 전송 실패 시 재시도 정책

## 6. 현재 리스크

- DB 비밀번호 평문 저장
- 카카오톡 전송은 여전히 포커스 의존형
- 삭제 실패 시 실제 발송과 DB 상태가 어긋날 수 있음
- 수동 진단과 자동 워커 결과가 서로 다른 원인으로 실패할 수 있으므로 구분해서 봐야 함
