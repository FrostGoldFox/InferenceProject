# 데이터 흐름 및 메시지 계약

## 로그인

```text
Client POST /client/login
  -> MiddleWare POST /login/client
  -> MainServer SELECT Login.HashPassword
  -> MiddleWare POST /loginResponse
  -> Client가 입력 비밀번호 해시와 비교
```

로그인 요청은 `type`, `id`, `hashpassword`를 사용합니다. 현재 토큰 발급·검증은 포함하지 않습니다.

## 검사 요청

```text
Client POST /Request (MainServer)
  -> MainServer가 Processing=true 등록
  -> POST /Request (MiddleWare)
  -> POST /message (InferenceServer)
```

```json
{
  "type": "request",
  "client": 1,
  "filename": "frame_1.jpg",
  "filelastnumber": 1,
  "filelength": 1024,
  "filedata": "BASE64_IMAGE"
}
```

InferenceServer는 정상 접수 시 `202 Accepted`를 반환하고 큐에서 비동기로 처리합니다. 모델은 서버 시작 시 한 번만 로드해 재사용합니다.

## 추론 결과

```text
InferenceServer POST /ResponSE (MiddleWare)
  -> POST /ResponSE (MainServer)
  -> POST /MainResponse (MiddleWare)
  -> POST /MainResponse (Client)
```

```json
{
  "type": "MainResponse",
  "ClientId": 1,
  "ProductName": "제품명",
  "SucessRate": "sucess",
  "Confidence": 0.8732,
  "Box": [120.5, 80.2, 410.8, 360.4]
}
```

- `Confidence`: 최고 신뢰도 검출 점수(`0.0~1.0`), 미검출 시 `null`
- `Box`: 최고 신뢰도 검출의 원본 이미지 픽셀 좌표 `[x1,y1,x2,y2]`, 미검출 시 `null`
- 임계값 이상 검출이 하나 이상이면 `SucessRate=sucess`, 없으면 `fail`
- 미검출 시 `ProductName=Unknown`

MainServer는 Client 전달이 성공하면 Processing을 `false`로 바꾸고 저장 대기 목록에 둡니다.

## DB 저장 및 조회

MainServer의 백그라운드 작업은 완료 결과를 최대 5건씩 트랜잭션으로 저장합니다. 5건 미만은 5초마다 부분 저장하며, 실패 시 메모리 목록으로 되돌려 재시도합니다. 중복 저장은 허용합니다.

AdminClient는 MySQL을 직접 조회해 최대 200건의 관리 기록, 기간 요약, 일별 추이, 최근 7일 성공·실패 막대 및 최근 1000건의 처리 결과 비율을 표시합니다. 관리 기록의 성공률 색상은 40% 이상 초록, 미만 빨강입니다.
