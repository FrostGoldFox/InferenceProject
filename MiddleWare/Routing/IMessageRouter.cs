using System.Text.Json;

namespace MiddleWare.Routing;

public interface IMessageRouter
{
    Task<object> RouteAsync(JsonElement message, CancellationToken cancellationToken = default);

    Task<object> RouteLoginAsync(
        JsonElement message,
        CancellationToken cancellationToken = default);

    Task<object> RouteLoginResponseAsync(
        JsonElement message,
        CancellationToken cancellationToken = default);

    Task<object> RouteToInferenceServerAsync(
        JsonElement message,
        CancellationToken cancellationToken = default);

    Task<object> RouteToMainServerAsync(
        JsonElement message,
        CancellationToken cancellationToken = default);

    Task<object> RouteToClientAsync(
        JsonElement message,
        CancellationToken cancellationToken = default);
}
