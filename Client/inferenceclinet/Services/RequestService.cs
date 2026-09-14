using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace inferenceclinet.Services;

// MainServer/Resource.cs의 RequestMessage와 동일한 필드 (DATAFLOW.md 확정 계약).
public sealed class RequestMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "request";

    [JsonPropertyName("client")]
    public int Client { get; set; }

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("filelastnumber")]
    public int Filelastnumber { get; set; }

    [JsonPropertyName("filelength")]
    public long Filelength { get; set; }

    [JsonPropertyName("filedata")]
    public string Filedata { get; set; } = string.Empty;
}

public interface IRequestService
{
    // ARCHITECTURE.md/DATAFLOW.md 기준: Middleware를 거치지 않고 MainServer(:5181)/Request에 직접 전송.
    Task SendAsync(RequestMessage request, CancellationToken ct = default);
}

public class RequestService : IRequestService
{
    private readonly HttpClient _httpClient;
    private const string RequestUrl = "Request"; // BaseAddress = AppConfig.MainServerBaseUrl

    public RequestService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task SendAsync(RequestMessage request, CancellationToken ct = default)
    {
        using var httpResponse = await _httpClient.PostAsJsonAsync(RequestUrl, request, ct);
        httpResponse.EnsureSuccessStatusCode();
    }
}
