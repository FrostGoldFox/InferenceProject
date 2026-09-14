using MySqlConnector;

namespace inferenceclinet.Data;

public class LoginRepository
{
    private static string BuildConnectionString()
    {
        var password = Environment.GetEnvironmentVariable("CLIENT_DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "CLIENT_DB_PASSWORD 환경변수가 설정되지 않았습니다.");
        }

        return new MySqlConnectionStringBuilder
        {
            Server = Environment.GetEnvironmentVariable("CLIENT_DB_HOST") ?? "127.0.0.1",
            Port = uint.TryParse(
                Environment.GetEnvironmentVariable("CLIENT_DB_PORT"),
                out var port)
                    ? port
                    : 3307,
            Database = Environment.GetEnvironmentVariable("CLIENT_DB_NAME") ?? "inference_db",
            UserID = Environment.GetEnvironmentVariable("CLIENT_DB_USER") ?? "inference_user",
            Password = password,
            SslMode = MySqlSslMode.Preferred
        }.ConnectionString;
    }

    public async Task<string?> GetHashPasswordAsync(string id, CancellationToken ct = default)
    {
        const string sql = "SELECT `HashPassword` FROM `Login` WHERE `ID` = @ID;";

        await using var connection = new MySqlConnection(BuildConnectionString());
        await connection.OpenAsync(ct);

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ID", id);

        var result = await command.ExecuteScalarAsync(ct);
        return result as string;
    }
}
