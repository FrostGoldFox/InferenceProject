# MiddleWare

ASP.NET Core 기반 HTTP JSON 중계 서버입니다.

| 수신 경로 | 전달 대상 |
|---|---|
| `POST /client/login` | MainServer `POST /login/client` |
| `POST /login/Response` | Client `POST /loginResponse` |
| `POST /Request`, `POST /api/router` | InferenceServer `POST /message` |
| `POST /ResponSE` | MainServer `POST /ResponSE` |
| `POST /MainResponse` | Client `POST /MainResponse` |

## 주소 설정

`appsettings.json`의 `Relay` 설정은 환경변수로 덮어쓸 수 있습니다.

- `Relay__InferenceServerBaseUrl`
- `Relay__MainServerBaseUrl`
- `Relay__ClientBaseUrl`
- `Relay__InferenceServerMessagePath`
- `Relay__MainServerResponsePath`
- `Relay__ClientResponsePath`
- `Relay__MainServerLoginPath`
- `Relay__ClientLoginResponsePath`

로컬 기본 대상은 각각 `localhost:8000`, `localhost:5181`, `localhost:5091`입니다.

## 제한

현재 구현은 메시지 중계와 기본 형식 검사에 집중합니다. 토큰 발급·검증, 요청별 권한 검사, 속도 제한 및 지속 세션 저장은 아직 포함하지 않습니다. ClientId별 세션 주소가 없으면 고정 `Relay__ClientBaseUrl`로 전달합니다.
