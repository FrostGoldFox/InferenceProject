# 시스템 아키텍처

## 구성 요소

- `Client`: WPF 로그인, 카메라 캡처, 검사 요청, 결과·신뢰도·Box 표시
- `MiddleWare`: 시스템 간 HTTP JSON 메시지를 고정 경로로 중계
- `MainServer`: 로그인 정보 조회, 처리 상태 관리, 결과 반환, MySQL 저장
- `InferenceServer`: 시작 시 모델을 한 번 로드하고 큐 기반으로 이미지 추론
- `AdminClient`: MySQL을 조회하여 관리 기록과 통계를 표시하는 WPF 프로그램
- `MySQL`: 로그인, 제품, Client 및 검사 성공률 데이터 저장

## 전체 연결

```text
Client(WPF/Camera)
  -> MainServer
  -> MiddleWare
  -> InferenceServer
  -> MiddleWare
  -> MainServer
  -> MiddleWare
  -> Client

MainServer -> MySQL <- AdminClient(WPF)
```

모듈 간 전송은 HTTP JSON을 사용하며, MySQL 연결에는 MySQL 프로토콜을 사용합니다. 서버 주소와 자격증명은 환경변수로 주입합니다.

## 책임 경계

- MainServer는 검사 결과를 저장하지만 관리자용 DB 조회 API는 제공하지 않습니다.
- AdminClient는 MySQL을 직접 조회하며 검사 데이터를 변경하지 않습니다.
- InferenceServer는 임계값 이상 검출이 하나 이상이면 `sucess`, 없으면 `fail`을 반환합니다.
- `SucessRate`와 `sucess` 철자는 기존 팀 계약과의 호환을 위해 유지합니다.
- AWS/Cloud 전송은 현재 실행 경로에 포함하지 않습니다.

## 배포

로컬 기본 주소는 모두 `localhost`입니다. 여러 PC에서 실행할 때 `ASPNETCORE_URLS`와 각 모듈의 주소 환경변수를 설정하고, 수신 포트의 OS 방화벽 및 네트워크 접근을 별도로 허용해야 합니다.
