using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace inferenceclinet.Services;

// Middleware의 client/login이 기대하는 정확한 필드/키 이름. 카멜케이스 변환에 기대지 않고 명시적으로 고정.
public sealed class LoginMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "login";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("hashpassword")]
    public string HashPassword { get; set; } = string.Empty;
}

public interface ILoginService
{
    // 이 호출은 MainServer의 비동기 조회를 트리거만 함 - 실제 결과는
    // ClientHttpHost가 받는 loginResponse push로 별도 도착함.
    Task LoginAsync(LoginMessage request, CancellationToken ct = default);
}

public class LoginService : ILoginService
{
    private readonly HttpClient _httpClient;
    private const string LoginUrl = "client/login"; // BaseAddress = AppConfig.MiddlewareBaseUrl

    public LoginService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task LoginAsync(LoginMessage request, CancellationToken ct = default)
    {
        using var httpResponse = await _httpClient.PostAsJsonAsync(LoginUrl, request, ct);
        httpResponse.EnsureSuccessStatusCode();
    }
}
