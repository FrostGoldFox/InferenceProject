using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace inferenceclinet.Services;

// Middleware가 Client로 push하는 두 콜백을 받기 위해 Client 안에서 여는 자체 HTTP 서버.
// - /loginResponse: login/Response -> loginResponse (docs/02-middleware-router-contract.md)
// - /MainResponse : InferenceServer 판정 결과 (docs/03-continuous-capture-plan.md, DATAFLOW.md)
public static class ClientHttpHost
{
    private static readonly object Lock = new();
    private static readonly Dictionary<string, TaskCompletionSource<string?>> PendingLogin = new();
    private static readonly Dictionary<string, TaskCompletionSource<MainResponsePayload?>> PendingMainResponse = new();
    private static WebApplication? _app;

    public static async Task EnsureStartedAsync(string listenUrl)
    {
        if (_app is not null)
        {
            return;
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(listenUrl);
        builder.Logging.ClearProviders();

        var app = builder.Build();

        app.MapPost("/loginResponse", (LoginResponsePayload payload) =>
        {
            CompletePendingLogin(payload.Id, payload.HashPassword);
            return Results.Ok();
        });

        app.MapPost("/MainResponse", (MainResponsePayload payload) =>
        {
            CompletePendingMainResponse(payload.ClientId.ToString(), payload);
            return Results.Ok();
        });

        await app.StartAsync();
        _app = app;
    }

    // MainServer가 조회한 저장된 HashPassword를 반환. timeout 시 null.
    public static Task<string?> WaitForLoginResponseAsync(string id, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (Lock)
        {
            PendingLogin[id] = tcs;
        }

        var cts = new CancellationTokenSource(timeout);
        cts.Token.Register(() =>
        {
            lock (Lock)
            {
                PendingLogin.Remove(id);
            }
            tcs.TrySetResult(null);
        });

        return tcs.Task;
    }

    // 특정 clientId에 대한 판정 결과(MainResponse)를 기다린다. timeout 시 null.
    public static Task<MainResponsePayload?> WaitForMainResponseAsync(string clientId, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<MainResponsePayload?>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (Lock)
        {
            PendingMainResponse[clientId] = tcs;
        }

        var cts = new CancellationTokenSource(timeout);
        cts.Token.Register(() =>
        {
            lock (Lock)
            {
                PendingMainResponse.Remove(clientId);
            }
            tcs.TrySetResult(null);
        });

        return tcs.Task;
    }

    private static void CompletePendingLogin(string id, string hashPassword)
    {
        TaskCompletionSource<string?>? tcs;
        lock (Lock)
        {
            if (!PendingLogin.Remove(id, out tcs))
            {
                return;
            }
        }

        tcs.TrySetResult(hashPassword);
    }

    private static void CompletePendingMainResponse(string clientId, MainResponsePayload payload)
    {
        TaskCompletionSource<MainResponsePayload?>? tcs;
        lock (Lock)
        {
            if (!PendingMainResponse.Remove(clientId, out tcs))
            {
                return;
            }
        }

        tcs.TrySetResult(payload);
    }
}

public sealed class LoginResponsePayload
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "loginResponse";

    [JsonPropertyName("HashPassword")]
    public string HashPassword { get; set; } = string.Empty;

    [JsonPropertyName("ID")]
    public string Id { get; set; } = string.Empty;
}

// MainServer/Resource.cs의 MainResponseMessage와 동일한 필드 (DATAFLOW.md 확정 계약, SucessRate/sucess 오탈자 유지).
public sealed class MainResponsePayload
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "MainResponse";

    [JsonPropertyName("ClientId")]
    public int ClientId { get; set; }

    [JsonPropertyName("ProductName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("SucessRate")]
    public string SucessRate { get; set; } = string.Empty;

    [JsonPropertyName("Confidence")]
    public double? Confidence { get; set; }

    // 최고 신뢰도 검출의 바운딩박스 [x1, y1, x2, y2] (원본 이미지 픽셀 기준). 미검출 시 null.
    // INFERENCE_BOX_API_CHANGE.md 참고. 좌표를 화면에 그리려면 원본 이미지 크기 대비 배율 계산이 추가로 필요(아직 미구현).
    [JsonPropertyName("Box")]
    public double[]? Box { get; set; }
}
