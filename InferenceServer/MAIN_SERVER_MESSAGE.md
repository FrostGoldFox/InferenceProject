# InferenceServer 결과 메시지

InferenceServer는 추론 완료 후 `MIDDLEWARE_RESPONSE_URL`로 HTTP JSON 콜백을 전송합니다. 기본값은 `http://localhost:5073/ResponSE`입니다.

```json
{
  "ClientId": 1,
  "ProductName": "제품명",
  "SucessRate": "sucess",
  "Confidence": 0.8732,
  "Box": [120.5, 80.2, 410.8, 360.4]
}
```

- `ClientId`: 요청의 `client`
- `ProductName`: 최고 신뢰도 클래스 이름에서 `|` 앞 부분
- `SucessRate`: 임계값 이상 검출이 있으면 `sucess`, 없으면 `fail`
- `Confidence`: 최고 검출 점수(`0.0~1.0`), 미검출 시 `null`
- `Box`: 원본 이미지 픽셀 기준 `[x1,y1,x2,y2]`, 미검출 시 `null`

`SucessRate` 및 `sucess` 철자는 기존 팀 계약 호환을 위해 유지합니다. 미검출 시 제품명은 `Unknown`입니다.

```text
InferenceServer
  -> MiddleWare POST /ResponSE
  -> MainServer POST /ResponSE
  -> MiddleWare POST /MainResponse
  -> Client POST /MainResponse
```
