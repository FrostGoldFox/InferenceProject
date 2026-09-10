# InferenceProject 현재 아키텍처

## 구성

- `MainServer`: Client 요청 상태 관리, MiddleWare 호출, 추론 결과 수신, MySQL 저장
- `MiddleWare`: 고정 목적지 기반 임시 1:1 HTTP JSON 중계
- `InferenceServer`: Python/FastAPI/PyTorch 이미지 추론 서버
- `Client`: WPF 스켈레톤만 존재
- `AdminClient`: 솔루션 프로젝트는 WPF 스켈레톤만 존재
- `AdminClientWpf`: 솔루션 외부에 있는 실제 관리자 조회 UI
- `MySQL`: Docker 컨테이너 `inference-mysql`, 데이터베이스 `inference_db`

로그인과 토큰 검증은 다른 담당자 범위이고 Cloud 연동은 후순위이므로 현재 구현 범위에서 제외한다.

## 현재 동작이 검증된 흐름

```text
가상 Client
  -> MainServer POST /Request
  -> MiddleWare POST /Request
  -> InferenceServer POST /message
  -> Base64 이미지 복원 및 추론
  -> MiddleWare POST /ResponSE
  -> MainServer POST /ResponSE
  -> MiddleWare POST /MainResponse
  -> 고정 Client POST /MainResponse
  -> MainServer가 완료 결과를 MySQL에 저장
```

Client 애플리케이션이 아직 비어 있으므로 실제 Client 구현 전에 최초 요청이 MainServer로 직접 들어갈지, MiddleWare가 MainServer로 중계할지 최종 확정해야 한다. 현재 구현과 통합 테스트는 MainServer 직접 진입 방식이다.

## 프로토콜

서버 간 통신은 HTTP JSON을 사용한다. MainServer와 MySQL 사이는 HTTP가 아니라 MySQL 드라이버 프로토콜이다. AdminClientWpf도 MySqlConnector로 MySQL에 직접 접속한다.

상세 계약은 [DATAFLOW.md](DATAFLOW.md), DB 스키마는 [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md)를 참고한다.

