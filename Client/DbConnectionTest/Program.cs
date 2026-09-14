using inferenceclinet.Data;
using MySqlConnector;

var password = Environment.GetEnvironmentVariable("CLIENT_DB_PASSWORD");
if (string.IsNullOrWhiteSpace(password))
{
    throw new InvalidOperationException("CLIENT_DB_PASSWORD 환경변수가 설정되지 않았습니다.");
}

var connectionString = new MySqlConnectionStringBuilder
{
    Server = Environment.GetEnvironmentVariable("CLIENT_DB_HOST") ?? "127.0.0.1",
    Port = uint.TryParse(Environment.GetEnvironmentVariable("CLIENT_DB_PORT"), out var port) ? port : 3307,
    Database = Environment.GetEnvironmentVariable("CLIENT_DB_NAME") ?? "inference_db",
    UserID = Environment.GetEnvironmentVariable("CLIENT_DB_USER") ?? "inference_user",
    Password = password,
    SslMode = MySqlSslMode.Preferred
}.ConnectionString;

Console.WriteLine("1) 원시 연결 테스트 (SELECT 1)");
await using (var connection = new MySqlConnection(connectionString))
{
    await connection.OpenAsync();
    await using var command = new MySqlCommand("SELECT 1;", connection);
    var pingResult = await command.ExecuteScalarAsync();
    Console.WriteLine($"   연결 성공, SELECT 1 결과 = {pingResult}");
}

Console.WriteLine("2) Login 테이블 내 ID 목록 (최대 5개)");
await using (var connection = new MySqlConnection(connectionString))
{
    await connection.OpenAsync();
    await using var command = new MySqlCommand("SELECT `ID` FROM `Login` LIMIT 5;", connection);
    await using var reader = await command.ExecuteReaderAsync();
    var found = false;
    while (await reader.ReadAsync())
    {
        found = true;
        Console.WriteLine($"   - {reader.GetString(0)}");
    }
    if (!found)
    {
        Console.WriteLine("   (Login 테이블에 데이터가 없습니다)");
    }
}

Console.WriteLine("3) LoginRepository.GetHashPasswordAsync 테스트");
Console.Write("   조회할 ID 입력 (Enter만 누르면 'testuser'): ");
var inputId = Console.ReadLine();
var id = string.IsNullOrWhiteSpace(inputId) ? "testuser" : inputId;

var repository = new LoginRepository();
var hash = await repository.GetHashPasswordAsync(id);
Console.WriteLine(hash is null
    ? $"   ID '{id}' 없음 (HashPassword = null)"
    : $"   ID '{id}'의 HashPassword = {hash}");
