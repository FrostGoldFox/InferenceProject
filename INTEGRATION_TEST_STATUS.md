# 통합 테스트 상태

## 확인 대상 흐름

- Client → MiddleWare → MainServer → MySQL 로그인 경로
- MainServer → MiddleWare → InferenceServer 요청 경로
- InferenceServer의 `202 Accepted` 및 큐 기반 처리
- InferenceServer → MiddleWare `/ResponSE` 콜백
- MiddleWare → MainServer → Client 결과 전달

## 배포 전 점검

1. 각 서버의 수신 주소를 `0.0.0.0` 또는 실제 인터페이스에 바인딩한다.
2. `README.md`의 환경변수로 실제 호스트 주소와 DB 접속정보를 주입한다.
3. OS 방화벽에서 필요한 수신 포트를 허용한다.
4. InferenceServer PC에서 MiddleWare 포트로 TCP 연결을 확인한다.
5. `/health`와 수동 JSON 요청으로 각 구간을 개별 검증한다.

## 보안 주의

테스트 계정, 비밀번호, 사설 IP 및 개인 PC의 절대경로는 저장소에 기록하지 않습니다. 테스트 계정은 시험 종료 후 폐기하거나 비밀번호를 변경합니다.

## 권장 점검 명령

```powershell
Test-NetConnection MIDDLEWARE_HOST -Port 5073
Invoke-RestMethod http://INFERENCE_HOST:8000/health
Invoke-RestMethod http://MAIN_SERVER_HOST:5181/health
```
