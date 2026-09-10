using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MiddleWare.Routing;

namespace MiddleWare.Controllers;

[ApiController]
public sealed class RouterController(
    IMessageRouter messageRouter,
    ILogger<RouterController> logger) : ControllerBase
{
    [HttpPost("Request")]
    [HttpPost("api/router")]
    [Consumes("application/json")]
    public Task<IActionResult> RelayRequest(
        [FromBody] JsonElement message,
        CancellationToken cancellationToken) =>
        ForwardAsync(
            () => messageRouter.RouteToInferenceServerAsync(message, cancellationToken),
            "InferenceServer");

    [HttpPost("ResponSE")]
    [Consumes("application/json")]
    public Task<IActionResult> InferenceResponse(
        [FromBody] JsonElement message,
        CancellationToken cancellationToken) =>
        ForwardAsync(
            () => messageRouter.RouteToMainServerAsync(message, cancellationToken),
            "MainServer");

    [HttpPost("MainResponse")]
    [Consumes("application/json")]
    public Task<IActionResult> MainResponse(
        [FromBody] JsonElement message,
        CancellationToken cancellationToken) =>
        ForwardAsync(
            () => messageRouter.RouteToClientAsync(message, cancellationToken),
            "Client");

    private async Task<IActionResult> ForwardAsync(
        Func<Task<object>> forward,
        string destination)
    {
        try
        {
            return Ok(await forward());
        }
        catch (OperationCanceledException exception)
        {
            logger.LogError(exception, "HTTP relay to {Destination} timed out.", destination);
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                error = $"Relay to {destination} timed out"
            });
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "HTTP relay to {Destination} failed.", destination);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = $"Relay to {destination} failed"
            });
        }
    }
}


