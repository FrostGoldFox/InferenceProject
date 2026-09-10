# 현재 데이터 흐름

## 1. 이미지 요청

Client 구현 전 통합 테스트에서 사용한 요청 경로다.

```text
Client -> MainServer /Request -> MiddleWare /Request -> InferenceServer /message
```

```json
{
  "type": "request",
  "client": 1,
  "filename": "image.jpg",
  "filelastnumber": 1,
  "filelength": 1024,
  "filedata": "Base64 이미지 데이터"
}
```

MainServer는 `List<Client>`에 `Processing=true` 객체를 lock 안에서 추가한다. MiddleWare 전달에 실패하면 추가한 객체를 제거한다. MiddleWare는 JSON을 변경하지 않고 InferenceServer로 전달한다.

## 2. 추론과 최종 응답

InferenceServer는 Base64를 이미지로 복원하고 서버 시작 시 한 번 로드한 Faster R-CNN 모델로 추론한다. 결과는 다음 JSON으로 변환한다.

```json
{
  "ClientId": 1,
  "ProductName": "KRPPL형",
  "SucessRate": "sucess"
}
```

```text
InferenceServer
  -> MiddleWare /ResponSE
  -> MainServer /ResponSE
  -> MiddleWare /MainResponse
  -> Client /MainResponse
```

Client 최종 메시지에는 `type: MainResponse`가 추가된다. MainServer는 Client 전달 성공 후 `Processing=false`로 바꾸며, 이후 DB 저장 대상이 된다.

`SucessRate`와 `sucess` 철자는 현재 확정된 호환 계약을 유지한다.

## 3. DB 저장

```text
MainServer -> MySqlConnector -> MySQL
```

- 대상 테이블: `ProductName`, `Client`, `SuccessRate`
- 완료 항목 최대 5건을 하나의 트랜잭션으로 저장한다.
- 5건 미만이면 현재 5초마다 부분 저장한다.
- 종료 시 남은 항목을 최대 10초 동안 flush한다.
- 저장 실패 시 메모리 목록에 되돌려 재시도한다.
- 중복 저장은 현재 허용한다.
- 외부 DB 조회 API는 제공하지 않는다.
- 저장 과정에서 ProductId 확보를 위한 내부 SELECT는 존재한다.

## 4. 관리자 조회

```text
AdminClientWpf -> MySqlConnector -> MySQL
```

별도 프로젝트 `C:\Users\user\AdminClientWpf`가 기록, 요약, 일별 추이, 최근 7일 통계를 조회한다. 솔루션 내부 `AdminClient`는 아직 빈 WPF 프로젝트다.

## 5. 현재 제외 범위

- 로그인 및 토큰 발급·검증
- Cloud/AWS EC2 전송
- DB 중복 방지

