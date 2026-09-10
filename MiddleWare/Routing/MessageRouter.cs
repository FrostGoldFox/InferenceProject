using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MiddleWare.Routing;

public sealed class MessageRouter(
    IHttpClientFactory httpClientFactory,
    IOptions<RelayOptions> options) : IMessageRouter
{
    private readonly RelayOptions _options = options.Value;

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
        CancellationToken cancellationToken = default) =>
        ForwardJsonAsync(
            _options.ClientBaseUrl,
            _options.ClientResponsePath,
            message,
            cancellationToken);

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


