# Client 담당 모듈 (① Login Module, ② Response Module)

담당: Client(`inferenceclinet`, WPF)
구간: ① Client ↔ Middleware ↔ MainServer Login Module (필수) / ② Client ↔ Middleware ↔ MainServer Response Module (① 완료 후 여유 있으면 진행)
일정: 09/09 ~ 09/11
※ Middleware는 Client / MainServer / Inference Server 제작이 모두 끝난 뒤 마지막에 제작 (그 전까지는 URL/스펙만 합의).

**우선순위: ①을 먼저 완성하고, 여유가 되면 ②를 시도.**

> 팀 스펙 미확정 구간은 표준안(초안)이며, 확정되면 본 문서에 반영합니다.

## 0. Client가 만드는 HTTP 구성요소 2가지

Client는 역할이 두 가지라 라우터/클라이언트를 구분해서 만든다.

| 구성요소 | 역할 | 방향 |
|---|---|---|
| Client → Middleware 호출 (HttpClient) | ① 로그인 요청 | Client가 호출자(caller) |
| **Client Router** (Client가 여는 HTTP 엔드포인트) | ② MainServer가 처리 완료한 결과를 Middleware 경유로 Client에 전달(push)받음 | Client가 수신자(listener) |

Middleware 쪽에도 별도로 **Middleware Router**가 있어야 하며 (Client 요청을 MainServer로 중계 + MainServer 결과를 Client로 중계), 이는 Middleware 담당자가 제작한다.

## 1. Login Module (①)

### 1-1. 시퀀스

```
Client                Middleware                MainServer(Login Module)
  |  1. POST /login       |                              |
  |----------------------->|  2. POST /login (forward)   |
  |                        |----------------------------->|
  |                        |                              | 3. 인증 처리
  |                        |  4. LoginResponse            |
  |                        |<-----------------------------|
  |  5. LoginResponse       |                              |
  |<-----------------------|                              |
  | 6. clientNo/token 저장 |                              |
```

### 1-2. API 스펙 (Client → Middleware → MainServer)

```
POST {MIDDLEWARE_BASE_URL}/api/auth/login
Content-Type: application/json
```

**Request**

| 필드 | 타입 | 설명 |
|---|---|---|
| clientId | string | 클라이언트(장비) 로그인 ID |
| password | string | 비밀번호 (또는 발급된 API Key) |
| deviceName | string? | 선택. 카메라/장비 식별용 표시 이름 |
| callbackUrl | string | Client Router 주소 (예: `http://192.168.0.10:6000`). Middleware가 ②에서 이 Client로 push할 때 사용 |

**Response**

| 필드 | 타입 | 설명 |
|---|---|---|
| success | bool | 로그인 성공 여부 |
| clientNo | string? | 성공 시 발급되는 클라이언트 번호. 이후 ②/③구간 요청에 함께 쓰임 |
| token | string? | 성공 시 발급되는 인증 토큰 |
| message | string? | 실패 사유 등 |

### 1-3. Token 발급 및 보유

- 로그인 성공 시 **MainServer가 Token을 발급**한다 (1-2절 응답의 `token` 필드).
- 이 Token은 **Client와 Middleware가 각각 보유**한다.
  - Client: 발급받은 `token`, `clientNo`를 세션/메모리에 저장하고, 이후 모든 요청에 `Authorization: Bearer {token}` 헤더로 실어 보낸다.
  - Middleware: 로그인 요청/응답이 중계되는 시점에 `token ↔ clientNo ↔ callbackUrl` 매핑을 자체 저장(캐시)해둔다. 이후 들어오는 요청마다 MainServer에 다시 물어보지 않고, Middleware 단에서 먼저 Token 유효성을 검사한다. `callbackUrl`은 ②에서 이 Client로 결과를 push할 때 목적지로 쓰인다.
- 이후(②, ③ 등) 모든 요청은 **Middleware가 Token을 먼저 검사**한 뒤 처리한다.
  - 검사 통과 → MainServer로 요청을 전달 (다음 단계 진행)
  - 검사 실패 → MainServer로 전달하지 않고, Middleware가 즉시 실패 응답을 반환한다.
- 실패 시 처리 방식은 **3. 인증 실패 처리 공통 규약** 참고.

### 1-4. C# 스텁

메서드는 **비동기 + `CancellationToken` 파라미터** 필수.

```csharp
public record LoginRequest(string ClientId, string Password, string CallbackUrl, string? DeviceName = null);
public record LoginResponse(bool Success, string? ClientNo, string? Token, string? Message);

public interface ILoginService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class LoginService : ILoginService
{
    private readonly HttpClient _httpClient;
    private const string LoginUrl = "/api/auth/login"; // BaseAddress = MIDDLEWARE_BASE_URL

    public LoginService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        using var httpResponse = await _httpClient.PostAsJsonAsync(LoginUrl, request, ct);
        httpResponse.EnsureSuccessStatusCode();
        var response = await httpResponse.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
        return response ?? new LoginResponse(false, null, null, "empty response");
    }
}
```

## 2. Response Module (②)

MainServer가 (③ Inference Server 결과를 받아) 처리를 끝내면, 그 결과를 Middleware를 거쳐 **Client에게 전달(push)**한다. Client는 이를 받기 위해 자체 HTTP 엔드포인트(Client Router)를 열어둔다.

> **확정 필요**: push 방식(콜백 HTTP POST) vs Client가 주기적으로 조회(polling)하는 방식 중 아직 미정. 아래는 push(콜백) 방식 기준 초안. 팀에서 polling으로 정해지면 1-2절 스타일로 다시 작성.

### 2-1. 시퀀스 (push 방식 가정)

```
InferenceServer   MainServer        Middleware              Client(Router)
      |----result------>|                |                        |
      |                 |--POST /result->| 1. Token 검증           |
      |                 |                |   (통과 시에만 아래 진행)|
      |                 |                |--POST /result--------->|
      |                 |                |                        | handle & 응답
      |                 |                |<--200 OK---------------|
      |                 |<--200 OK-------|                        |
```

Middleware가 Client로 전달하기 전 Token을 먼저 검증한다 (1-3절 참고). 검증 실패 시 Client로 전달하지 않고 실패 응답을 되돌린다 — 실패 처리 방식은 **3. 인증 실패 처리 공통 규약** 참고.

### 2-2. API 스펙 (MainServer/Middleware → Client Router)

```
POST {CLIENT_ROUTER_URL}/api/result
Content-Type: application/json
```

**Request (Client 입장에선 수신 payload)**

| 필드 | 타입 | 설명 |
|---|---|---|
| serialId | string | 검사 대상 제품 일련번호 |
| clientId | string | 요청한 클라이언트 번호 (①에서 발급받은 clientNo) |
| result | object | 조립 검사 결과 (아래 참고) |

`result` 세부 (③ 스케치의 "제품명 / 클라이언트번호 / 성공여부"에 대응):

| 필드 | 타입 | 설명 |
|---|---|---|
| success | bool | 정상 조립 여부 |
| productName | string? | 인식된 제품/부품 이름 |
| message | string? | 실패 사유 등 (선택) |

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

### 2-3. C# 스텁

메서드는 **비동기 + 파라미터 `serialId`, `clientId`, `result` (+ 필요 시 추가 필드)**.

```csharp
public record ResultPayload(bool Success, string? ProductName, string? Message);

public interface IResultReceiver
{
    Task HandleResultAsync(string serialId, string clientId, ResultPayload result, CancellationToken ct = default);
}

public class ResultReceiver : IResultReceiver
{
    public Task HandleResultAsync(string serialId, string clientId, ResultPayload result, CancellationToken ct = default)
    {
        // TODO: UI 갱신, 로컬 이력 저장 등
        return Task.CompletedTask;
    }
}
```

**Client Router(수신 엔드포인트) 자리 표시** — 실제 호스팅 방식(Kestrel 내장 vs HttpListener 등)은 팀과 협의 후 결정:

```csharp
// 예: ASP.NET Core minimal API를 WPF 내부에서 self-host
app.MapPost("/api/result", async (ResultNotification body, IResultReceiver receiver, CancellationToken ct) =>
{
    await receiver.HandleResultAsync(body.SerialId, body.ClientId, body.Result, ct);
    return Results.Ok();
});

public record ResultNotification(string SerialId, string ClientId, ResultPayload Result);
```

## 3. 인증 실패 처리 공통 규약

Token 검증(①②, 이후 다른 구간도 동일)이 실패했을 때 **어떤 이유로 실패했는지 타입으로 구분**할 수 있어야 한다. 방식: 실패 타입을 미리 enum으로 정의해두고, 실패 시 그 타입을 담은 예외(Exception)를 throw 한다 (Python의 `raise`와 동일한 패턴).

### 3-1. 실패 타입 정의

```csharp
public enum AuthErrorType
{
    InvalidToken,    // 토큰이 없거나 형식이 잘못됨
    TokenExpired,    // 토큰 만료
    ClientMismatch,  // 토큰과 clientId가 일치하지 않음
    Unknown          // 그 외
}
```

### 3-2. 실패 응답 스키마 (Middleware → Client)

Token 검증 실패 시 Middleware는 MainServer로 전달하지 않고 아래 형태로 즉시 응답한다.

```json
{
  "success": false,
  "errorType": "TokenExpired",
  "message": "token expired at 2026-09-10T09:00:00Z"
}
```

### 3-3. 커스텀 예외 (Client 측)

```csharp
public class AuthException : Exception
{
    public AuthErrorType ErrorType { get; }

    public AuthException(AuthErrorType errorType, string? message = null)
        : base(message ?? errorType.ToString())
    {
        ErrorType = errorType;
    }
}
```

### 3-4. Client의 HTTP 래퍼: 실패 응답 → 예외 변환

Client가 호출하는 모든 인증 필요 API(②, 추후 다른 API 포함)는 공통 래퍼를 통해 실패 응답을 감지하면 그 자리에서 `AuthException`을 throw 한다. 호출부는 타입(`ErrorType`)으로 실패 원인을 구분해 처리한다.

```csharp
public record AuthErrorResponse(bool Success, AuthErrorType ErrorType, string? Message);

public static class HttpResponseExtensions
{
    public static async Task EnsureAuthSuccessAsync(this HttpResponseMessage response, CancellationToken ct = default)
    {
        if (response.IsSuccessStatusCode) return;

        var error = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(cancellationToken: ct);
        throw new AuthException(error?.ErrorType ?? AuthErrorType.Unknown, error?.Message);
    }
}
```

사용 예시:

```csharp
try
{
    using var httpResponse = await _httpClient.PostAsJsonAsync(ResultUrl, request, ct);
    await httpResponse.EnsureAuthSuccessAsync(ct);
}
catch (AuthException ex) when (ex.ErrorType == AuthErrorType.TokenExpired)
{
    // 재로그인 유도 등
}
```

## 4. 확정 필요 항목 (TODO)

- [ ] ② 방식: push(콜백) vs polling 최종 결정
- [ ] Client Router 호스팅 방식 (Kestrel self-host / HttpListener 등) 및 포트
- [ ] Middleware/MainServer 실제 라우트 경로 확정
- [ ] 인증 방식: token(JWT) vs session
- [ ] `result` 필드 최종 스키마 (추가 필드 필요 시)
- [ ] `MIDDLEWARE_BASE_URL`, `CLIENT_ROUTER_URL` 등 설정값 (appsettings.json 분리)
- [ ] `AuthErrorType` 목록이 팀(Middleware/MainServer) 스펙과 일치하는지 확인
- [ ] `callbackUrl`(Client Router 주소)을 로그인 때 같이 등록하는 방식이 팀 스펙과 맞는지 확인 (Middleware가 ② push 대상 주소를 알아야 하므로 추가함)

## 5. 관련 문서

- [02-middleware-router-contract.md](02-middleware-router-contract.md) — 위 내용을 Middleware Router에 그대로 추가할 수 있게 정리한 계약서 (Middleware 담당자 전달용)
