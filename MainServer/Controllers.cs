using Microsoft.AspNetCore.Mvc;

namespace MainServer;

[ApiController]
[Route("Request")]
public sealed class RequestController(
    ClientService clientService,
    MiddleWareClient middleWareClient,
    ILogger<RequestController> logger) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> ReceiveRequest(
        [FromBody] RequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Type, "request", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "type must be request" });
        }

        var trackedClient = clientService.AddProcessingClient(request.Client);

        try
        {
            await middleWareClient.SendRequestAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            clientService.RemoveClient(trackedClient);
            throw;
        }
        catch (OperationCanceledException exception)
        {
            clientService.RemoveClient(trackedClient);
            logger.LogError(exception, "MiddleWare request timed out. ClientId: {ClientId}", request.Client);
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                error = "MiddleWare request timed out",
                client = request.Client
            });
        }
        catch (HttpRequestException exception)
        {
            clientService.RemoveClient(trackedClient);
            logger.LogError(exception, "MiddleWare request failed. ClientId: {ClientId}", request.Client);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "MiddleWare request failed",
                client = request.Client
            });
        }

        return Accepted(new
        {
            client = request.Client,
            filename = request.Filename,
            processing = true
        });
    }
}

[ApiController]
[Route("login/client")]
public sealed class LoginController(
    LoginRepository loginRepository,
    MiddleWareClient middleWareClient,
    ILogger<LoginController> logger) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> ReceiveLogin(
        [FromBody] LoginClientMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Type, "login", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "type must be login" });
        }

        var storedHash = await loginRepository.GetHashPasswordAsync(request.Id, cancellationToken);

        var response = new LoginResponseMessage
        {
            HashPassword = storedHash ?? string.Empty,
            Id = request.Id
        };

        try
        {
            await middleWareClient.SendLoginResponseAsync(response, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            logger.LogError(exception, "MiddleWare login/Response timed out. Id: {Id}", request.Id);
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                error = "MiddleWare login/Response timed out"
            });
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "MiddleWare login/Response failed. Id: {Id}", request.Id);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "MiddleWare login/Response failed"
            });
        }

        return Ok(new { status = "forwarded" });
    }
}

[ApiController]
[Route("ResponSE")]
public sealed class ResponseController(
    ClientService clientService,
    MiddleWareClient middleWareClient,
    ILogger<ResponseController> logger) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> ReceiveResponse(
        [FromBody] ResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!IsResultValueValid(response.SucessRate))
        {
            return BadRequest(new { error = "SucessRate must be fail or sucess" });
        }

        if (!clientService.IsProcessingClient(response.ClientId))
        {
            return NotFound(new
            {
                error = "Processing ClientId was not found",
                response.ClientId
            });
        }

        var mainResponse = new MainResponseMessage
        {
            ClientId = response.ClientId,
            ProductName = response.ProductName,
            SucessRate = response.SucessRate
        };

        try
        {
            await middleWareClient.SendMainResponseAsync(mainResponse, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            logger.LogError(exception, "MiddleWare MainResponse timed out. ClientId: {ClientId}", response.ClientId);
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                error = "MiddleWare MainResponse timed out",
                response.ClientId
            });
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "MiddleWare MainResponse failed. ClientId: {ClientId}", response.ClientId);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "MiddleWare MainResponse failed",
                response.ClientId
            });
        }

        if (!clientService.CompleteProcessingClient(
                response.ClientId,
                response.ProductName,
                response.SucessRate))
        {
            logger.LogWarning(
                "Client result was delivered but state completion failed. ClientId: {ClientId}",
                response.ClientId);
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                error = "Client state completion failed",
                response.ClientId
            });
        }

        return Ok(mainResponse);
    }

    private static bool IsResultValueValid(string value) =>
        string.Equals(value, "fail", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "sucess", StringComparison.OrdinalIgnoreCase);
}




