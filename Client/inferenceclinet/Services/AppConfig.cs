namespace inferenceclinet.Services;

public static class AppConfig
{
    public static string MiddlewareBaseUrl =>
        GetString("MIDDLEWARE_BASE_URL", "http://localhost:5073");

    public static string ClientListenUrl =>
        GetString("CLIENT_LISTEN_URL", "http://localhost:5091");

    public static string MainServerBaseUrl =>
        GetString("MAIN_SERVER_BASE_URL", "http://localhost:5181");

    public static int NumericClientId => GetPositiveInt("CLIENT_ID", 1);

    public static int CaptureIntervalMs => GetPositiveInt("CAPTURE_INTERVAL_MS", 400);
    public static int ResponseWaitTimeoutMs => GetPositiveInt("RESPONSE_WAIT_TIMEOUT_MS", 5000);

    private static string GetString(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static int GetPositiveInt(string name, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }
}
