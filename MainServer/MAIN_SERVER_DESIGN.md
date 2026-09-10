# MainServer 실제 구현 설계

## 책임

MainServer는 현재 Client 작업 상태와 최종 결과의 MySQL 저장을 담당한다. 외부 조회 API, 로그인, Cloud 연동은 담당 범위에서 제외한다.

## 파일

- `Program.cs`: Controller, DatabaseOptions, ClientService, ResultRepository, ResultBatchWorker, MiddleWareClient 등록
- `Resource.cs`: Client 상태 및 Request/Response/MainResponse/Database DTO
- `Controllers.cs`: `/Request`, `/ResponSE` 엔드포인트
- `NetWork.cs`: MiddleWare `/Request`, `/MainResponse` 호출
- `Services.cs`: 메모리 Client 상태, MySQL 저장, 배치 Worker

## 상태 흐름

```text
/Request
  -> Client(Processing=true) 추가
  -> MiddleWare 전달
  -> 실패 시 해당 Client 제거

/ResponSE
  -> Processing 상태 Client 확인
  -> MiddleWare를 통해 Client에 최종 결과 전달
  -> 성공 시 Processing=false
  -> ResultBatchWorker 저장 대상
```

Client 목록은 `List<Client>`이며 모든 추가·조회·변경·제거는 하나의 lock 안에서 수행한다.

## DB 저장

`ResultBatchWorker`는 500ms마다 완료 목록을 확인한다. 5건이 모이면 즉시 저장하고, 5건 미만은 5초마다 저장한다. 하나의 배치는 하나의 MySQL 트랜잭션이다. 실패한 배치는 Client 목록에 되돌린다.

현재 구현은 원래 설계의 `Channel<T>` 대신 사용자가 지정한 `clients.Where(n => n.Processing == false)` 기반 목록 폴링 방식을 사용한다.

## 알려진 제한

- 상태와 대기 결과는 메모리에만 있어 프로세스 종료 시 유실될 수 있다.
- 여러 MainServer 인스턴스 간 상태 공유가 없다.
- DB 장애 재시도에 백오프가 없다.
- 중복 저장은 허용한다.

