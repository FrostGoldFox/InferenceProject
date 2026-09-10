using System.Net.Http.Json;

namespace MainServer;

public sealed class MiddleWareClient(HttpClient httpClient)
{
    public Task SendRequestAsync(
        RequestMessage request,
        CancellationToken cancellationToken) =>
        PostJsonAsync("Request", request, cancellationToken);

    public Task SendMainResponseAsync(
        MainResponseMessage response,
        CancellationToken cancellationToken) =>
        PostJsonAsync("MainResponse", response, cancellationToken);

    public Task SendLoginResponseAsync(
        LoginResponseMessage response,
        CancellationToken cancellationToken) =>
        PostJsonAsync("login/Response", response, cancellationToken);

    private async Task PostJsonAsync<T>(
        string path,
        T message,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            path,
            message,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
