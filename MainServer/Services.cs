using Microsoft.Extensions.Options;
using MySqlConnector;

namespace MainServer;

public sealed class ClientService
{
    private readonly object _clientsLock = new();
    private readonly List<Client> _clients = [];

    public Client AddProcessingClient(int clientId)
    {
        lock (_clientsLock)
        {
            var client = new Client
            {
                ClientId = clientId,
                Processing = true
            };
            _clients.Add(client);
            return client;
        }
    }

    public void RemoveClient(Client client)
    {
        lock (_clientsLock)
        {
            _clients.Remove(client);
        }
    }

    public bool IsProcessingClient(int clientId)
    {
        lock (_clientsLock)
        {
            return _clients.Any(client =>
                client.ClientId == clientId &&
                client.Processing);
        }
    }

    public bool CompleteProcessingClient(
        int clientId,
        string productName,
        string sucessRate)
    {
        lock (_clientsLock)
        {
            var client = _clients.Find(item =>
                item.ClientId == clientId &&
                item.Processing);

            if (client is null)
            {
                return false;
            }

            client.ProductName = productName;
            client.SucessRate = sucessRate;
            client.Processing = false;
            return true;
        }
    }

    public IReadOnlyList<Client> TakeCompletedClients(
        int maximumCount,
        bool allowPartialBatch)
    {
        lock (_clientsLock)
        {
            var completedClients = _clients
                .Where(client => client.Processing == false)
                .Where(client => client.ProductName is not null)
                .Where(client => client.SucessRate is not null)
                .Take(maximumCount)
                .ToArray();

            if (!allowPartialBatch && completedClients.Length < maximumCount)
            {
                return [];
            }

            foreach (var client in completedClients)
            {
                _clients.Remove(client);
            }

            return completedClients;
        }
    }

    public void ReturnCompletedClients(IEnumerable<Client> clients)
    {
        lock (_clientsLock)
        {
            _clients.AddRange(clients);
        }
    }

    public IReadOnlyList<Client> GetSnapshot()
    {
        lock (_clientsLock)
        {
            return _clients
                .Select(client => new Client
                {
                    ClientId = client.ClientId,
                    Processing = client.Processing,
                    ProductName = client.ProductName,
                    SucessRate = client.SucessRate
                })
                .ToArray();
        }
    }
}

public sealed class LoginRepository(IOptions<DatabaseOptions> options)
{
    private readonly DatabaseOptions _options = options.Value;

    public async Task<string?> GetHashPasswordAsync(string id, CancellationToken cancellationToken)
    {
        var connectionString = new MySqlConnectionStringBuilder
        {
            Server = _options.Host,
            Port = _options.Port,
            Database = _options.Database,
            UserID = _options.User,
            Password = _options.Password,
            SslMode = MySqlSslMode.Preferred
        }.ConnectionString;

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `HashPassword` FROM `Login` WHERE `ID` = @ID;";
        command.Parameters.AddWithValue("@ID", id);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }
}

public sealed class ResultRepository(
    IOptions<DatabaseOptions> options,
    ILogger<ResultRepository> logger)
{
    private readonly DatabaseOptions _options = options.Value;

    public async Task SaveAsync(
        IReadOnlyCollection<Client> clients,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                "Database password is missing. Set Database__Password.");
        }

        var connectionString = new MySqlConnectionStringBuilder
        {
            Server = _options.Host,
            Port = _options.Port,
            Database = _options.Database,
            UserID = _options.User,
            Password = _options.Password,
            SslMode = MySqlSslMode.Preferred
        }.ConnectionString;

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var client in clients)
            {
                await EnsureClientAsync(connection, transaction, client, cancellationToken);
                var productId = await GetOrCreateProductAsync(
                    connection,
                    transaction,
                    client.ProductName!,
                    cancellationToken);

                await InsertSuccessRateAsync(
                    connection,
                    transaction,
                    client,
                    productId,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Saved {Count} completed clients to MySQL.", clients.Count);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task EnsureClientAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        Client client,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO `Client` (`ClientId`, `ClientName`)
            VALUES (@ClientId, @ClientName)
            ON DUPLICATE KEY UPDATE `ClientId` = `ClientId`;
            """;
        command.Parameters.AddWithValue("@ClientId", client.ClientId);
        command.Parameters.AddWithValue("@ClientName", $"Client-{client.ClientId}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ulong> GetOrCreateProductAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string productName,
        CancellationToken cancellationToken)
    {
        await using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText = """
            SELECT `ProductId`
            FROM `ProductName`
            WHERE `ProductName` = @ProductName
            ORDER BY `ProductId`
            LIMIT 1;
            """;
        selectCommand.Parameters.AddWithValue("@ProductName", productName);

        var existingId = await selectCommand.ExecuteScalarAsync(cancellationToken);
        if (existingId is not null)
        {
            return Convert.ToUInt64(existingId);
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT INTO `ProductName` (`ProductName`)
            VALUES (@ProductName);
            """;
        insertCommand.Parameters.AddWithValue("@ProductName", productName);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        return (ulong)insertCommand.LastInsertedId;
    }

    private static async Task InsertSuccessRateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        Client client,
        ulong productId,
        CancellationToken cancellationToken)
    {
        var succeeded = string.Equals(
            client.SucessRate,
            "sucess",
            StringComparison.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO `SuccessRate`
                (`ClientId`, `ProductId`, `SuccessCount`, `FailureCount`)
            VALUES
                (@ClientId, @ProductId, @SuccessCount, @FailureCount);
            """;
        command.Parameters.AddWithValue("@ClientId", client.ClientId);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@SuccessCount", succeeded ? 1 : 0);
        command.Parameters.AddWithValue("@FailureCount", succeeded ? 0 : 1);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class ResultBatchWorker(
    ClientService clientService,
    ResultRepository repository,
    ILogger<ResultBatchWorker> logger) : BackgroundService
{
    private const int BatchSize = 5;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PartialBatchFlushInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextPartialFlush = DateTimeOffset.UtcNow + PartialBatchFlushInterval;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var allowPartialBatch = DateTimeOffset.UtcNow >= nextPartialFlush;
                var clients = clientService.TakeCompletedClients(BatchSize, allowPartialBatch);

                if (clients.Count > 0)
                {
                    await SaveOrReturnAsync(clients, stoppingToken);
                }

                if (allowPartialBatch)
                {
                    nextPartialFlush = DateTimeOffset.UtcNow + PartialBatchFlushInterval;
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }

        await FlushRemainingAsync();
    }

    private async Task SaveOrReturnAsync(
        IReadOnlyList<Client> clients,
        CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveAsync(clients, cancellationToken);
        }
        catch (Exception exception)
        {
            clientService.ReturnCompletedClients(clients);
            logger.LogError(
                exception,
                "Failed to save {Count} completed clients. They were returned for retry.",
                clients.Count);
        }
    }

    private async Task FlushRemainingAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (!timeout.IsCancellationRequested)
        {
            var clients = clientService.TakeCompletedClients(BatchSize, allowPartialBatch: true);
            if (clients.Count == 0)
            {
                return;
            }

            try
            {
                await repository.SaveAsync(clients, timeout.Token);
            }
            catch (Exception exception)
            {
                clientService.ReturnCompletedClients(clients);
                logger.LogError(exception, "Shutdown flush failed.");
                return;
            }
        }
    }
}


