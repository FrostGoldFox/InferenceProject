# Client

WPF 카메라 클라이언트로 예정된 프로젝트다.

## 현재 상태

- .NET 10 WPF 기본 스켈레톤만 존재
- UI, 카메라 캡처, Base64 변환, HTTP 전송 미구현
- `POST /MainResponse`를 받을 HTTP 수신 기능 미구현

서버 통합 테스트에서는 Client 대신 `http://localhost:5091/MainResponse` 일회성 수신기를 사용했다.

## 구현 전 확정 사항

현재 동작 검증은 Client가 MainServer `POST /Request`로 직접 요청하는 흐름이다. 기존 상위 설계에는 Client가 MiddleWare에 먼저 접속하는 흐름도 남아 있으므로 최초 진입 경로를 확정해야 한다.

요청 JSON에는 `type`, `client`, `filename`, `filelastnumber`, `filelength`, `filedata`가 필요하며 `filedata`는 Base64 이미지다.

