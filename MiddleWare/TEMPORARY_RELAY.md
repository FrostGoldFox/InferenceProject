# MiddleWare 임시 1:1 중계

로그인 및 토큰 검증 모듈이 완성되기 전까지 MiddleWare는 수신한 JSON을 검사·변환하지 않고 고정된 HTTP 목적지로 전달한다.

## 중계 경로

| 발신 | MiddleWare 수신 경로 | 목적지 |
|---|---|---|
| MainServer | `POST /Request` | InferenceServer `POST /message` |
| InferenceServer | `POST /ResponSE` | MainServer `POST /ResponSE` |
| MainServer | `POST /MainResponse` | Client `POST /MainResponse` |

`POST /api/router`는 기존 호환을 위해 InferenceServer `POST /message`로 전달한다.

모든 요청 본문은 `application/json`만 사용한다. 목적지 서버가 2xx가 아닌 응답을 반환하거나 연결에 실패하면 MiddleWare는 `502 Bad Gateway`를 반환한다.

## 기본 주소

- MiddleWare: `http://localhost:5073`
- InferenceServer: `http://localhost:8000`
- MainServer: `http://localhost:5181`
- Client: `http://localhost:5091` (Client 구현 시 실제 수신 주소로 변경)

목적지 주소와 경로는 `appsettings.json`의 `Relay` 섹션에서 변경한다.

## 보류 항목

- 로그인 요청 중계
- 로그인 토큰 발급
- 토큰 유효성 및 권한 검증
- ClientId별 동적 Client 주소 관리

현재 Client가 하나라는 전제이므로 모든 `MainResponse`는 `Relay:ClientBaseUrl` 한 곳으로 전달한다. 여러 Client를 연결할 때에는 ClientId와 접속 주소를 매핑하는 방식으로 교체해야 한다.
