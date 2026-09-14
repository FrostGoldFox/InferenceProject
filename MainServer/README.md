# MainServer

ASP.NET Core 기반 요청 상태 및 DB 저장 서버입니다.

## API

- `POST /Request`: Base64 이미지 요청 접수 및 MiddleWare 전달
- `POST /login/client`: Login 테이블의 비밀번호 해시 조회 후 Client로 중계
- `POST /ResponSE`: 추론 결과 접수, Client 전달 및 저장 대기 전환
- `GET /health`: 서버 상태 확인

## 저장

완료 결과는 백그라운드 작업이 최대 5건씩 MySQL 트랜잭션으로 저장합니다. 5건 미만은 5초마다 부분 저장하며, DB 오류 시 메모리 목록으로 되돌려 재시도합니다. MainServer는 관리자용 조회 API를 제공하지 않습니다.

## 환경변수

- `ASPNETCORE_URLS`: 외부 수신 시 예: `http://0.0.0.0:5181`
- `MiddleWare__BaseUrl`: 기본 `http://localhost:5073/`
- `Database__Host`: 기본 `127.0.0.1`
- `Database__Port`: 기본 `3307`
- `Database__Database`: 기본 `inference_db`
- `Database__User`: 기본 `inference_user`
- `Database__Password`: 필수

```powershell
dotnet run --project .\MainServer.csproj
```

비밀번호와 배포 주소는 소스 및 `appsettings.json`에 저장하지 않습니다.
