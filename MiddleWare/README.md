# MiddleWare

로그인 모듈이 완성되기 전까지 사용하는 ASP.NET Core 기반 고정 1:1 HTTP JSON 중계 서버다.

## 현재 경로

| 수신 경로 | 목적지 |
|---|---|
| `POST /api/auth/login` | MainServer Login Module (`Relay:MainServerLoginPath`), 성공 시 `clientId ↔ token ↔ callbackUrl` 세션 저장 |
| `POST /Request` | InferenceServer `POST /message` |
| `POST /api/router` | InferenceServer `POST /message` |
| `POST /ResponSE` | MainServer `POST /ResponSE` |
| `POST /MainResponse` | 로그인 시 등록된 clientId별 콜백 주소(`{callbackUrl}/api/result`), 세션이 없으면 기존 고정 `Relay:ClientBaseUrl`로 폴백 |

기본 주소는 다음과 같다.

- MiddleWare: `http://localhost:5073`
- InferenceServer: `http://localhost:8000`
- MainServer: `http://localhost:5181`
- Client: `http://localhost:5091`

목적지는 `appsettings.json`의 `Relay`에서 변경한다. 다운스트림 연결 또는 비정상 HTTP 응답은 `502 Bad Gateway`로 변환한다.

## 현재 제한

- 로그인은 중계하고 세션(token/clientNo/callbackUrl)을 저장하지만, **토큰 유효성 검증(만료/위조 등)은 아직 강제하지 않음** — `/Request`, `/ResponSE`, `/MainResponse` 어디에도 인증 체크가 없음
- 세션은 인메모리(`ClientSessionStore`)라 프로세스 재시작 시 유실됨
- 재시도와 회로 차단 없음
- JSON 내용에 대한 업무 스키마 검증 없음

토큰 검증(만료/위조 거부, `AuthErrorType` 기반 에러 응답)은 Client 측 계약서(`docs/02-middleware-router-contract.md`, inferenceclinet 저장소) B절 참고, 아직 미구현.

