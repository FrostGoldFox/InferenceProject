using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MiddleWare.Auth;

namespace MiddleWare.Routing;

public sealed class MessageRouter(
    IHttpClientFactory httpClientFactory,
    IOptions<RelayOptions> options,
    IClientSessionStore sessionStore) : IMessageRouter
{
    private readonly RelayOptions _options = options.Value;

    // Client -> Middleware(client/login) -> MainServer(login/client). type 필드는 컨트롤러에서 이미 "login"으로 확인함.
    public Task<object> RouteLoginAsync(
        JsonElement message,
        CancellationToken cancellationToken = default) =>
        ForwardJsonAsync(
            _options.MainServerBaseUrl,
            _options.MainServerLoginPath,
            message,
            cancellationToken);

    // MainServer -> Middleware(login/Response) -> Client(loginResponse). Client가 자체 검증 후 결과 판단.
    public Task<object> RouteLoginResponseAsync(
        JsonElement message,
        CancellationToken cancellationToken = default) =>
        ForwardJsonAsync(
            _options.ClientBaseUrl,
            _options.ClientLoginResponsePath,
            message,
            cancellationToken);

    public Task<object> RouteAsync(
        JsonElement message,
        CancellationToken cancellationToken = default)
    {
        if (!message.TryGetProperty("type", out var typeProperty))
        {
            throw new ArgumentException("type field is required.", nameof(message));
        }

        var type = typeProperty.GetString();

        return type switch
        {
            "reset" or "request" or "file" =>
                RouteToInferenceServerAsync(message, cancellationToken),
            "MainResponse" =>
                RouteToClientAsync(message, cancellationToken),
            _ => throw new ArgumentException($"Unsupported message type: {type}", nameof(message))
        };
    }

    public Task<object> RouteToInferenceServerAsync(
        JsonElement message,
        CancellationToken cancellationToken = default) =>
        ForwardJsonAsync(
            _options.InferenceServerBaseUrl,
            _options.InferenceServerMessagePath,
            message,
            cancellationToken);

    public Task<object> RouteToMainServerAsync(
        JsonElement message,
        CancellationToken cancellationToken = default) =>
        ForwardJsonAsync(
            _options.MainServerBaseUrl,
            _options.MainServerResponsePath,
            message,
            cancellationToken);

    public Task<object> RouteToClientAsync(
        JsonElement message,
        CancellationToken cancellationToken = default)
    {
        // clientId로 로그인 시 등록된 콜백 주소를 찾으면 그쪽으로, 못 찾으면 기존 고정 주소로 폴백.
        // (TODO: 여러 Client 연결이 실제로 붙으면 폴백 없이 실패 처리할지 팀과 협의)
        var baseUrl = _options.ClientBaseUrl;
        var path = _options.ClientResponsePath;

        if (message.TryGetProperty("clientId", out var clientIdProperty) &&
            clientIdProperty.GetString() is { } clientId &&
            sessionStore.TryGetByClientNo(clientId, out var session))
        {
            baseUrl = session.CallbackUrl;
            path = "api/result";
        }

        return ForwardJsonAsync(baseUrl, path, message, cancellationToken);
    }

    private async Task<object> ForwardJsonAsync(
        string baseUrl,
        string path,
        JsonElement message,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("Relay");
        var destination = new Uri(new Uri(EnsureTrailingSlash(baseUrl)), path.TrimStart('/'));

        using var response = await client.PostAsJsonAsync(destination, message, cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength == 0)
        {
            return new { status = "forwarded" };
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new { status = "forwarded" };
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch (JsonException)
        {
            return new { status = "forwarded", response = content };
        }
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith('/') ? value : $"{value}/";
}


