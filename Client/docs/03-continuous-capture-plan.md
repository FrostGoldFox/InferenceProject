# 연속 촬영 전송 실행계획

목표: 카메라로 초당 여러 번 사진을 찍어 각각 MainServer에 전송하고, 판정 결과를 실시간으로 검사이력에 쌓는다.

## 1. 전송 경로 (실제 검증된 코드 기준)

`ARCHITECTURE.md` / `DATAFLOW.md`(InferenceProject 저장소)에 이미 검증된 흐름이 명시돼 있다 — 로그인(①)과 달리 이미지 전송은 **Middleware를 거치지 않고 Client → MainServer 직접**이다.

```
이미지 전송: Client --POST /Request--> MainServer(:5181) --POST /Request--> MiddleWare --POST /message--> InferenceServer
결과 수신:  InferenceServer --/ResponSE--> MiddleWare --/ResponSE--> MainServer --/MainResponse--> MiddleWare --/MainResponse--> Client
```

> `ARCHITECTURE.md`에는 "최초 요청이 MainServer로 직접 들어갈지, MiddleWare가 중계할지 최종 확정해야 한다"고 적혀 있지만, 현재 통합 테스트는 **직접 진입 방식**으로 검증돼 있다. 이 계획도 그 방식을 따른다 — 바뀌면 이 문서도 갱신.

## 2. 메시지 스펙 (MainServer 실제 코드 그대로, 임의 변경 금지)

**Request (Client → MainServer)**

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

**MainResponse (Client가 수신)**

```json
{
  "type": "MainResponse",
  "ClientId": 1,
  "ProductName": "KRPPL형",
  "SucessRate": "sucess"
}
```

`SucessRate`/`sucess` 철자는 오탈자가 아니라 확정된 호환 계약이므로 그대로 사용한다 (`DATAFLOW.md` 명시).

## 3. MainServer 쪽 제약 — 연속 전송 시 반드시 고려

`MainServer/Services.cs`의 `ClientService`는 `List<Client>`에 `Processing=true` 상태를 쌓는 구조다.

- `AddProcessingClient`는 같은 clientId라도 매번 새 항목을 추가한다 (중복 방지 없음).
- 응답이 오면 `IsProcessingClient(clientId)`는 "존재 여부"만 확인하고, `CompleteProcessingClient`는 **첫 번째로 찾은 항목**을 완료 처리한다.
- 즉, 같은 clientId로 응답을 기다리는 요청이 **동시에 여러 개 쌓이면 어떤 프레임의 결과인지 구분할 방법이 없다** — 프레임이 뒤섞여 잘못 매칭될 수 있다.
- `filelastnumber` 필드가 프레임 구분용으로 보이지만, 현재 MainServer 코드는 이 값을 전혀 읽지 않는다.

**결론**: "찍는 즉시 다음 프레임도 바로 전송"하는 완전 동시 방식은 피하고, **한 프레임을 보낸 뒤 결과(or 타임아웃)를 받고 나서 다음 프레임을 전송**하는 순차 루프로 구현한다. 왕복 지연이 짧으면 이것만으로도 "초당 여러 번" 요건은 자연스럽게 달성된다.

## 4. 구현 단계

### 4-1. 카메라 캡처 계층
- NuGet: `OpenCvSharp4`, `OpenCvSharp4.runtime.win`
- `Services/CameraCaptureService.cs`: `VideoCapture`로 웹캠 열기 → 프레임 읽기 → JPEG 인코딩(byte[])

### 4-2. MonitorWindow 연동
- Cam 박스에 실시간 미리보기 바인딩 (프레임 → `BitmapImage` 변환 후 `Image` 컨트롤에 표시)
- `분석` 버튼(`OnAnalyzeClick`, 현재 TODO): 캡처→전송 루프 시작/중지 토글
- `중지` 버튼(`OnStopClick`, 현재 TODO): 루프 취소(`CancellationTokenSource`)

### 4-3. 전송 루프
- `Services/RequestService.cs` 신설: `RequestMessage` 생성 → `HttpClient`로 MainServer `:5181/Request`에 POST
- 순서: 캡처 → 인코딩 → 전송 → (③의 제약에 따라) 응답 또는 타임아웃 대기 → 다음 캡처 반복
- 전송 주기는 설정값으로 (`AppConfig.CaptureIntervalMs`, 우선 300~500ms 제안 — 팀 확인 필요)

### 4-4. 결과 수신
- `ClientHttpHost`(①에서 이미 만든 자체 HTTP 서버)에 `/MainResponse` 라우트 추가 — `/loginResponse`와 동일 패턴
- 수신 시 `InspectionRecord`로 변환해 MonitorWindow 검사이력 `DataGrid`에 추가

### 4-5. 문서 정리
- [01-client-modules.md](01-client-modules.md)의 "② Response Module" 절은 MainServer/Middleware 실물이 없던 시점에 제가 임의로 잡은 초안(`/api/result`, `ResultPayload` 등)이라 지금 확인된 실제 계약(`MainResponse`, `ClientId`/`ProductName`/`SucessRate`)과 다르다. 이 기능 구현 후 해당 절을 실제 계약으로 갱신 필요.

## 5. 결정 필요 (TODO)

- [ ] 전송 주기(초당 몇 회) 최종 값
- [ ] Client → MainServer 직접 진입 방식이 팀 내 최종 확정인지 재확인 (`ARCHITECTURE.md`가 아직 "확정 필요"로 표시돼 있음)
- [ ] `filelastnumber`를 프레임 매칭용으로 실제 활용할지 MainServer 담당자와 협의 — 안 하면 3번 문제(응답 뒤섞임)가 남는다
- [ ] 연속 캡처 중 네트워크 끊김/타임아웃 시 재시도 정책
- [ ] 카메라 장치가 여러 개일 때 선택 UI 필요 여부
