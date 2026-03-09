# 온보딩 문서

## 1. 프로젝트 개요

이 프로젝트는 두 축으로 구성됩니다.

- PostgreSQL에 접속해 데이터를 확인하거나 이후 메시지 원본을 조회할 수 있는 WinForms 클라이언트
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
  - DB 설정/연결 테스트/조회/메시지 전송 버튼 제공
- `ClientSettings.cs`
  - 설정 모델
- `SettingsStore.cs`
  - `client-settings.json` 읽기/쓰기
- `PostgresClient.cs`
  - Npgsql 기반 PostgreSQL 연결과 조회 실행
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
4. `Run Query`로 SQL 실행 후 결과를 그리드에 표시

### 카카오톡 쪽

1. 사용자가 채팅방 이름과 메시지를 입력
2. `Send Message` 클릭
3. 메인 카카오톡 창을 찾음
4. `Ctrl+F` 검색, 채팅방 열기, 메시지 전송 시퀀스 수행

## 4. 설정 파일

설정은 실행 폴더의 `client-settings.json`에 저장됩니다.

주의:

- PostgreSQL 비밀번호가 평문으로 저장됩니다.
- 배포 전 암호화나 Windows 자격 증명 저장소 사용 여부를 검토할 필요가 있습니다.

## 5. 다음 확장 포인트

- 특정 테이블/쿼리 결과를 메시지 전송 입력값에 자동 연결
- DB 폴링 또는 작업 큐 처리
- 메시지 발송 이력 저장
- 설정 암호화
- 전송 실패 시 재시도 정책

## 6. 현재 리스크

- DB 비밀번호 평문 저장
- 카카오톡 전송은 여전히 포커스 의존형
- 사용자가 직접 실행하는 SQL은 안전장치가 없음
