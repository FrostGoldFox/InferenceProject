# InferenceProject

카메라 이미지 수집, HTTP 중계, AI 추론, 결과 저장 및 관리자 조회를 하나의 저장소에서 관리하는 프로젝트입니다.

## 구성

- `Client`: WPF 카메라 Client 및 보조 도구
- `MiddleWare`: ASP.NET Core HTTP JSON 중계 서버
- `MainServer`: 요청 상태 관리, 로그인 조회, 결과 전달 및 MySQL 저장
- `InferenceServer`: FastAPI/PyTorch 추론 서버
- `AdminClient`: WPF 관리자 기록·통계 조회 프로그램

## 기본 실행 주소

로컬 개발 기본값은 `MiddleWare http://localhost:5073`, `MainServer http://localhost:5181`, `InferenceServer http://localhost:8000`, `Client callback http://localhost:5091`입니다. 여러 PC에 배포할 때는 아래 환경변수만 배포 환경에 맞게 지정합니다.

### MainServer

```powershell
$env:ASPNETCORE_URLS = "http://0.0.0.0:5181"
$env:MiddleWare__BaseUrl = "http://MIDDLEWARE_HOST:5073/"
$env:Database__Host = "MYSQL_HOST"
$env:Database__Port = "3307"
$env:Database__Database = "inference_db"
$env:Database__User = "inference_user"
$env:Database__Password = "CHANGE_ME"
dotnet run --project .\MainServer\MainServer.csproj
```

### MiddleWare

```powershell
$env:ASPNETCORE_URLS = "http://0.0.0.0:5073"
$env:Relay__InferenceServerBaseUrl = "http://INFERENCE_HOST:8000/"
$env:Relay__MainServerBaseUrl = "http://MAIN_SERVER_HOST:5181/"
$env:Relay__ClientBaseUrl = "http://CLIENT_HOST:5091/"
dotnet run --project .\MiddleWare\MiddleWare.csproj
```

### InferenceServer

```powershell
$env:INFERENCE_CHECKPOINT_PATH = "C:\path\to\checkpoint.pt"
$env:INFERENCE_DEVICE = "auto"
$env:INFERENCE_SCORE_THRESHOLD = "0.4"
$env:MIDDLEWARE_RESPONSE_URL = "http://MIDDLEWARE_HOST:5073/ResponSE"
python .\InferenceServer\main.py
```

### Client

```powershell
$env:MIDDLEWARE_BASE_URL = "http://MIDDLEWARE_HOST:5073"
$env:MAIN_SERVER_BASE_URL = "http://MAIN_SERVER_HOST:5181"
$env:CLIENT_LISTEN_URL = "http://0.0.0.0:5091"
$env:CLIENT_ID = "1"
$env:CAPTURE_INTERVAL_MS = "400"
$env:RESPONSE_WAIT_TIMEOUT_MS = "5000"
dotnet run --project .\Client\inferenceclinet\inferenceclinet.csproj
```

Client의 `CLIENT_DB_HOST`, `CLIENT_DB_PORT`, `CLIENT_DB_NAME`, `CLIENT_DB_USER`, `CLIENT_DB_PASSWORD`는 DB 연결 시험 도구에서 사용합니다. 실제 WPF 로그인은 MiddleWare와 MainServer를 경유합니다.

### AdminClient

```powershell
$env:ADMINCLIENT_DB_HOST = "MYSQL_HOST"
$env:ADMINCLIENT_DB_PORT = "3307"
$env:ADMINCLIENT_DB_NAME = "inference_db"
$env:ADMINCLIENT_DB_USER = "inference_user"
$env:ADMINCLIENT_DB_PASSWORD = "CHANGE_ME"
dotnet run --project .\AdminClient\AdminClient.csproj
```

비밀번호, 모델 파일, `.env` 파일 및 개인별 설정은 Git에 커밋하지 않습니다.

## 빌드

```powershell
dotnet build .\InferenceProject.slnx
```

상세 흐름은 `ARCHITECTURE.md`, `DATAFLOW.md`, DB 구조는 `DATABASE_SCHEMA.md`를 참고합니다.
