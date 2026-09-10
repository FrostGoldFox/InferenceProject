# MiddleWare

로그인 모듈이 완성되기 전까지 사용하는 ASP.NET Core 기반 고정 1:1 HTTP JSON 중계 서버다.

## 현재 경로

| 수신 경로 | 목적지 |
|---|---|
| `POST /Request` | InferenceServer `POST /message` |
| `POST /api/router` | InferenceServer `POST /message` |
| `POST /ResponSE` | MainServer `POST /ResponSE` |
| `POST /MainResponse` | 고정 Client `POST /MainResponse` |

기본 주소는 다음과 같다.

- MiddleWare: `http://localhost:5073`
- InferenceServer: `http://localhost:8000`
- MainServer: `http://localhost:5181`
- Client: `http://localhost:5091`

목적지는 `appsettings.json`의 `Relay`에서 변경한다. 다운스트림 연결 또는 비정상 HTTP 응답은 `502 Bad Gateway`로 변환한다.

## 현재 제한

- 로그인·토큰 검증 없음
- 모든 엔드포인트 인증 없음
- ClientId별 주소 관리 없음
- 모든 최종 응답을 하나의 Client 주소로 전달
- 재시도와 회로 차단 없음
- JSON 내용에 대한 업무 스키마 검증 없음

로그인과 보안 검사는 다른 담당자 구현 후 이 중계 흐름 앞에 결합해야 한다.

