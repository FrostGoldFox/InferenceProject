# MainServer

ASP.NET Core Web API 기반 상태 관리 및 DB 저장 서버다.

## 현재 구현

- `POST /Request`: Base64 이미지 JSON 수신
- ClientId와 `Processing=true` 상태를 lock으로 관리
- 요청을 MiddleWare `POST /Request`로 전달
- 전달 실패 시 방금 추가한 Client 상태 제거
- `POST /ResponSE`: InferenceServer 판정 수신
- MiddleWare를 통해 고정 Client `POST /MainResponse`로 결과 전달
- Client 전달 성공 후 `Processing=false`로 전환
- 완료 결과를 최대 5건씩 MySQL 트랜잭션 저장
- 5초 부분 flush 및 서버 종료 flush
- DB 실패 시 메모리 목록으로 반환
- `GET /health`

DB 조회 API, 로그인, Cloud, 중복 방지는 현재 범위에서 제외한다. 제품 저장을 위해 내부적으로 ProductName에서 ProductId를 찾는 SELECT는 사용한다.

## 실행 설정

- HTTP: `http://localhost:5181`
- MiddleWare: `http://localhost:5073`
- DB 비밀번호: 코드에 저장하지 않고 `Database__Password` 환경변수 사용
- MySQL 기본 주소: `127.0.0.1:3307/inference_db`

## 남은 보완

- 메모리 상태의 서버 재시작 내구성
- DB 장애 시 지수 백오프
- 요청별 상관관계 ID와 구조화 오류 계약
- 자동 단위·통합 테스트
- Client 최초 진입 경로 최종 확정

