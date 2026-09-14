# Client

로그인, 카메라 연속 캡처, 검사 요청 및 결과 표시를 담당하는 WPF 프로그램입니다.

## 환경변수

- `MIDDLEWARE_BASE_URL`: 기본 `http://localhost:5073`
- `MAIN_SERVER_BASE_URL`: 기본 `http://localhost:5181`
- `CLIENT_LISTEN_URL`: 기본 `http://localhost:5091`
- `CLIENT_ID`: 기본 `1`
- `CAPTURE_INTERVAL_MS`: 기본 `400`
- `RESPONSE_WAIT_TIMEOUT_MS`: 기본 `5000`

DB 시험 도구는 `CLIENT_DB_HOST`, `CLIENT_DB_PORT`, `CLIENT_DB_NAME`, `CLIENT_DB_USER`, `CLIENT_DB_PASSWORD`를 사용합니다.

## 실행

```powershell
dotnet run --project .\inferenceclinet\inferenceclinet.csproj
```

Client는 한 프레임을 전송한 뒤 해당 ClientId의 결과 또는 시간 초과를 기다리고 다음 프레임을 처리합니다. 결과 화면에는 제품명, 성공/실패, 최고 신뢰도 및 Box 좌표를 표시합니다.
