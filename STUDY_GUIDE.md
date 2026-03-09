# 학습 가이드

## 현재 구조를 이해하는 가장 빠른 순서

1. `MainForm.cs`
2. `MessageDispatchService.cs`
3. `ChatOutRepository.cs`
4. `SettingsStore.cs`
5. `DispatchLogStore.cs`
6. `PostgresClient.cs`
7. `MessageSender.cs`
8. `Win32.cs`

## 1. 왜 WinForms 클라이언트로 바꿨나

기존 CLI는 테스트에는 충분했지만, 실제 DB 연결 정보를 수시로 바꾸고 자동 워커를 통제하기에는 불편했습니다.

지금 구조는 다음 목적에 맞춰 바뀌었습니다.

- DB 접속 정보를 화면에서 수정
- 연결 테스트를 즉시 수행
- 워커를 시작/중지
- 대기열 상태를 눈으로 확인
- `Ctrl+F`와 수동 단건 전송을 별도로 진단

## 2. PostgreSQL 연결 구조

`PostgresClient.cs`는 Npgsql 드라이버를 사용합니다.

핵심 역할:

- 연결 문자열 생성
- 연결 테스트
- 연결 객체 생성

`ChatOutRepository.cs`는 이 연결을 사용해 `chat_out`를 읽고 지웁니다.

## 3. 대기열 처리 구조

현재 워커는 아래 순서로 동작합니다.

1. 1초마다 tick
2. 이미 처리 중이면 이번 tick 무시
3. `chat_out`에서 `msg_id ASC LIMIT 1` 조회
4. 카카오톡 전송 수행
5. 성공 판정 모듈 호출
6. 성공이면 즉시 delete
7. `dispatch-log.jsonl`에 시간 로그 append
8. 실패면 워커 중지

이렇게 하면 Spring Scheduler처럼 “이전 작업이 끝나기 전에는 다시 안 돈다”는 모델을 단순하게 구현할 수 있습니다.

## 4. 설정 저장 구조

`SettingsStore.cs`는 `client-settings.json`을 읽고 씁니다.

장점:

- 실행 파일 폴더 기준으로 동작해서 배포가 단순함
- 운영자가 UI에서 설정을 저장하고 바로 재사용 가능

단점:

- 비밀번호가 평문 저장됨

`DispatchLogStore.cs`는 `dispatch-log.jsonl`에 건별 발송 시간을 저장합니다.

기록 기준:

- 시작: DB에서 해당 메시지를 폴링한 시점
- 종료: 카카오톡 전송 시퀀스 완료 시점
- 차이: `duration_ms`, `duration_sec`

## 5. 카카오톡 자동화 구조

자동화 코어는 기존과 비슷하게 유지됩니다.

- `ChatFinder.cs`
  - 카카오톡 메인 창 찾기
- `MessageSender.cs`
  - 검색 열기
  - 채팅방 선택
  - 메시지 붙여넣기
  - 전송

즉, UI는 워커를 담당하고 실제 전송은 별도 모듈이 담당합니다.

## 6. 성공 판정 추상화

`ISendConfirmationPolicy`로 성공 판정을 분리했습니다.

현재 구현체는 `SequenceCompletedConfirmationPolicy`이고,
기준은 단순합니다.

- 전송 시퀀스가 예외 없이 끝났으면 성공

나중에는 이 부분만 교체해서
- UI 버블 확인
- OCR 확인
- 더 강한 확인 로직
으로 확장할 수 있습니다.

## 7. 실무적으로 먼저 보강할 부분

- DB 비밀번호 보관 방식
- `chat_out` 상태 컬럼 도입 여부
- 발송 성공/실패 로그 저장
- 카카오톡 포커스 안정화

## 8. 현재 디버깅 포인트

`v10` 기준으로는 자동 워커와 별개로 수동 진단 경로가 있습니다.

- `Test Ctrl+F`
  - 검색 단축키만 확인
- `Send Manual Message`
  - 워커 없이 단건 메시지 전송 확인

이 둘을 먼저 확인하면
- 카카오톡 입력 자체 문제인지
- DB 폴링/삭제 워커 문제인지
를 분리해서 볼 수 있습니다.
