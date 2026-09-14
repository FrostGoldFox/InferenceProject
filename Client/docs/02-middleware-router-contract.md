# Middleware Router 계약서 (Client 담당 구간 ①②)

> **대상**: Middleware 담당자. 이 문서 내용을 Middleware의 Router에 추가해주세요.
> 원본 상세 스펙은 [01-client-modules.md](01-client-modules.md) 참고 (여기서는 Router 관점으로만 요약).

Middleware Router가 Client 관련해서 처리해야 할 라우트는 3개다.

| # | 라우트 | 방향 | 설명 |
|---|---|---|---|
| A | `POST /api/auth/login` | Client → Middleware → MainServer | 로그인 중계 |
| B | (모든 인증 필요 요청 공통) | Client/MainServer → Middleware | Token 검증 |
| C | `POST /api/result` | MainServer → Middleware → Client | 검사 결과 push 중계 |

## A. `POST /api/auth/login` (Client → Middleware)

Client가 이 주소로 로그인을 요청한다. Middleware는 **그대로 MainServer Login Module로 forward**하고, 응답을 그대로 Client에 돌려주면 된다 (Middleware가 추가로 검증할 로직은 없음).

**Request** (Client로부터 그대로 수신, MainServer로 그대로 전달)

```json
{
  "clientId": "cam-001",
  "password": "xxxxx",
  "deviceName": "라인1-카메라",
  "callbackUrl": "http://192.168.0.10:6000"
}
```

**Response** (MainServer로부터 받은 것을 그대로 Client에 전달)

```json
{
  "success": true,
  "clientNo": "CN-2026-0001",
  "token": "eyJhbGciOi...",
  "message": null
}
```

**Middleware가 이 라우트에서 추가로 해야 할 일**: 응답이 `success: true`면, 아래 매핑을 캐시(메모리/Redis 등)에 저장해둔다. 이후 B, C에서 사용한다.

```
token → { clientNo, callbackUrl }
```

## B. Token 검증 (공통 미들웨어/필터)

로그인(A) 이외의 모든 요청은 처리 전에 Token을 검증한다.

1. 요청 헤더에서 `Authorization: Bearer {token}` 추출
2. A에서 저장해둔 캐시에 해당 token이 있는지 확인
   - 없거나 형식이 잘못됨 → `InvalidToken`
   - 있지만 만료됨 → `TokenExpired`
   - token은 있는데 요청의 clientId와 캐시된 clientNo가 다름 → `ClientMismatch`
3. 통과 → 다음 단계(MainServer로 forward, 또는 C처럼 Client로 forward) 진행
4. 실패 → 다음 단계로 넘어가지 않고 즉시 아래 형식으로 응답

```json
{
  "success": false,
  "errorType": "TokenExpired",
  "message": "token expired at 2026-09-10T09:00:00Z"
}
```

`errorType`은 아래 값 중 하나 (Client 쪽과 이름이 동일해야 함):

```csharp
public enum AuthErrorType
{
    InvalidToken,
    TokenExpired,
    ClientMismatch,
    Unknown
}
```

## C. `POST /api/result` (MainServer → Middleware → Client)

MainServer가 검사 결과를 Middleware로 보내면, Middleware는 B의 Token 검증(이 경우 `clientId`로 캐시를 찾아 유효한 클라이언트인지 확인)을 거친 뒤, 캐시에 저장된 **`callbackUrl` + `/api/result`** 로 그대로 forward 한다.

**Request/전달 payload** (MainServer → Middleware → Client, 동일한 형식)

```json
{
  "serialId": "SN-2026-000123",
  "clientId": "CN-2026-0001",
  "result": {
    "success": true,
    "productName": "브래킷-A",
    "message": null
  }
}
```

**Middleware가 이 라우트에서 해야 할 일**:
1. `clientId`로 캐시에서 `callbackUrl` 조회 (없으면 `InvalidToken` 등으로 MainServer에 에러 응답)
2. `POST {callbackUrl}/api/result` 로 위 payload를 그대로 전달
3. Client의 응답(200 OK 등)을 그대로 MainServer에 반환

## 시퀀스 요약

```
Client            Middleware                  MainServer          (③ Inference Server)
  |--A:login------->|--forward-------------------->|
  |<--LoginResponse--|<------------------------------|
  |  (token 캐시 저장: token→{clientNo, callbackUrl})|
  |                  |                               |
  |                  |<--C:result(clientId,...)-------| <--- 결과 (③에서 옴)
  |                  | B: clientId로 callbackUrl 조회  |
  |<--C:result-------|                               |
  |--200 OK--------->|--200 OK---------------------->|
```

## 확정 필요 (TODO — [01-client-modules.md](01-client-modules.md) 4번과 동일)

- [ ] Token 캐시 저장소 (메모리 vs Redis 등) 및 만료 정책
- [ ] `AuthErrorType` 값이 MainServer와도 동일한지
- [ ] Middleware/MainServer 실제 라우트 경로 최종 확정
- [ ] push(C) 실패 시(Client가 꺼져있는 등) 재시도 정책
