# AdminClient

MySQL의 검사 기록과 통계를 조회하는 WPF 관리자 프로그램입니다.

## 화면

1. 로그인
2. 관리 기록: 기간별 최대 200건, Client·제품·결과 정보 및 성공률 표시
3. 통계: 누적 접속 Client, 일별 요청 수, 최근 7일 성공·실패, 최근 1000건 결과 비율

관리 기록 성공률은 40% 이상이면 초록, 미만이면 빨강으로 표시합니다.

## DB 환경변수

- `ADMINCLIENT_DB_PASSWORD`: 필수
- `ADMINCLIENT_DB_HOST`: 기본 `127.0.0.1`
- `ADMINCLIENT_DB_PORT`: 기본 `3307`
- `ADMINCLIENT_DB_NAME`: 기본 `inference_db`
- `ADMINCLIENT_DB_USER`: 기본 `inference_user`

```powershell
dotnet run --project .\AdminClient.csproj
```

AdminClient는 조회 전용 계정을 사용하는 것을 권장합니다.
