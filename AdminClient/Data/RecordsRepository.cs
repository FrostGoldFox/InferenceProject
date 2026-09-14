using System.Windows.Media;
using MySqlConnector;

namespace AdminClientUI.Data;

/// <summary>
/// inference_db(Docker inference-mysql, MYSQL_CONNECTION.md 참고) 조회 전용 리포지토리.
/// 계정은 inference_user(앱 계정)를 쓴다 — root는 쓰지 않는다.
/// </summary>
public class RecordsRepository
{
    private static readonly Brush HealthyBackground = new SolidColorBrush(Color.FromRgb(226, 247, 235));
    private static readonly Brush HealthyForeground = new SolidColorBrush(Color.FromRgb(27, 135, 82));
    private static readonly Brush UnhealthyBackground = new SolidColorBrush(Color.FromRgb(255, 232, 233));
    private static readonly Brush UnhealthyForeground = new SolidColorBrush(Color.FromRgb(201, 54, 59));

    // start는 포함, endExclusive는 미포함 — 호출부에서 "종료일 다음날 0시"를 넘겨 종료일 하루 전체를 포함시킨다.
    public async Task<List<RecordItem>> GetRecentRecordsAsync(DateTime start, DateTime endExclusive, int limit = 200)
    {
        var records = new List<RecordItem>();

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT sr.CreatedAt, c.ClientName, p.ProductName, sr.SuccessCount, sr.FailureCount, sr.SuccessRate
            FROM SuccessRate sr
            JOIN Client c ON c.ClientId = sr.ClientId
            JOIN ProductName p ON p.ProductId = sr.ProductId
            WHERE sr.CreatedAt >= @start AND sr.CreatedAt < @end
            ORDER BY sr.CreatedAt DESC
            LIMIT @limit
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@start", start);
        command.Parameters.AddWithValue("@end", endExclusive);
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var createdAt = reader.GetDateTime("CreatedAt");
            var clientName = reader.GetString("ClientName");
            var productName = reader.GetString("ProductName");
            var successCount = reader.GetInt32("SuccessCount");
            var failureCount = reader.GetInt32("FailureCount");
            var successRate = reader.GetDecimal("SuccessRate");

            // 성공률이 40% 이상이면 초록, 40% 미만이면 빨강으로 표시한다.
            var isHealthy = successRate >= 40m;

            records.Add(new RecordItem(
                createdAt.ToString("yyyy-MM-dd HH:mm:ss"),
                clientName,
                productName,
                $"성공 {successCount} · 실패 {failureCount}",
                $"{successRate:0.00}%",
                isHealthy ? HealthyBackground : UnhealthyBackground,
                isHealthy ? HealthyForeground : UnhealthyForeground));
        }

        return records;
    }

    public async Task<SummaryCounts> GetSummaryAsync(DateTime start, DateTime endExclusive)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT
              COALESCE(SUM(SuccessCount + FailureCount), 0) AS TotalCount,
              COALESCE(SUM(SuccessCount), 0) AS SuccessCount,
              COALESCE(SUM(FailureCount), 0) AS FailureCount,
              COUNT(DISTINCT ClientId) AS ActiveClientCount
            FROM SuccessRate
            WHERE CreatedAt >= @start AND CreatedAt < @end
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@start", start);
        command.Parameters.AddWithValue("@end", endExclusive);
        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new SummaryCounts(
                reader.GetInt32("TotalCount"),
                reader.GetInt32("SuccessCount"),
                reader.GetInt32("FailureCount"),
                reader.GetInt32("ActiveClientCount"));
        }

        return new SummaryCounts(0, 0, 0, 0);
    }

    public async Task<ResultRatioCounts> GetRecentResultRatioAsync(int limit = 1000)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT
              COALESCE(SUM(SuccessCount + FailureCount), 0) AS TotalCount,
              COALESCE(SUM(SuccessCount), 0) AS SuccessCount,
              COALESCE(SUM(FailureCount), 0) AS FailureCount
            FROM (
              SELECT SuccessCount, FailureCount
              FROM SuccessRate
              ORDER BY CreatedAt DESC, SuccessRateId DESC
              LIMIT @limit
            ) recent
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@limit", limit);
        await using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new ResultRatioCounts(
                reader.GetInt32("TotalCount"),
                reader.GetInt32("SuccessCount"),
                reader.GetInt32("FailureCount"));
        }

        return new ResultRatioCounts(0, 0, 0);
    }

    // startDay/endDay는 둘 다 날짜(자정) 기준, 양끝 포함.
    public async Task<List<DailyCount>> GetDailyTrendAsync(DateTime startDay, DateTime endDay)
    {
        var byDay = new SortedDictionary<DateTime, int>();
        for (var day = startDay.Date; day <= endDay.Date; day = day.AddDays(1))
        {
            byDay[day] = 0;
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT DATE(CreatedAt) AS Day, SUM(SuccessCount + FailureCount) AS Total
            FROM SuccessRate
            WHERE CreatedAt >= @start AND CreatedAt < @end
            GROUP BY DATE(CreatedAt)
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@start", startDay.Date);
        command.Parameters.AddWithValue("@end", endDay.Date.AddDays(1));

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            byDay[reader.GetDateTime("Day")] = reader.GetInt32("Total");
        }

        return byDay.Select(kv => new DailyCount(kv.Key, kv.Value)).ToList();
    }

    private static MySqlConnection CreateConnection()
    {
        var password = Environment.GetEnvironmentVariable("ADMINCLIENT_DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "ADMINCLIENT_DB_PASSWORD 환경변수가 설정되지 않았습니다.");
        }

        var connectionString = new MySqlConnectionStringBuilder
        {
            Server = Environment.GetEnvironmentVariable("ADMINCLIENT_DB_HOST") ?? "127.0.0.1",
            Port = uint.TryParse(
                Environment.GetEnvironmentVariable("ADMINCLIENT_DB_PORT"),
                out var port)
                ? port
                : 3307,
            Database = Environment.GetEnvironmentVariable("ADMINCLIENT_DB_NAME") ?? "inference_db",
            UserID = Environment.GetEnvironmentVariable("ADMINCLIENT_DB_USER") ?? "inference_user",
            Password = password,
            SslMode = MySqlSslMode.Preferred
        }.ConnectionString;

        return new MySqlConnection(connectionString);
    }

    public async Task<List<DailyBreakdown>> GetLast7DaysAsync()
    {
        var today = DateTime.Today;
        var byDay = new SortedDictionary<DateTime, (int Success, int Failure)>();
        for (var i = 6; i >= 0; i--)
        {
            byDay[today.AddDays(-i)] = (0, 0);
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT DATE(CreatedAt) AS Day, SUM(SuccessCount) AS SuccessTotal, SUM(FailureCount) AS FailureTotal
            FROM SuccessRate
            WHERE CreatedAt >= @since
            GROUP BY DATE(CreatedAt)
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@since", today.AddDays(-6));

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            byDay[reader.GetDateTime("Day")] = (reader.GetInt32("SuccessTotal"), reader.GetInt32("FailureTotal"));
        }

        return byDay.Select(kv => new DailyBreakdown(kv.Key, kv.Value.Success, kv.Value.Failure)).ToList();
    }
}
