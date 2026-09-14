using System.Windows.Media;

namespace AdminClientUI.Data;

public sealed record RecordItem(
    string Time,
    string ClientName,
    string ProductName,
    string ResultText,
    string SuccessRateText,
    Brush StatusBackground,
    Brush StatusForeground);

public sealed record SummaryCounts(int TotalCount, int SuccessCount, int FailureCount, int ActiveClientCount);

public sealed record ResultRatioCounts(int TotalCount, int SuccessCount, int FailureCount);

public sealed record DailyCount(DateTime Date, int Total);

public sealed record DailyBreakdown(DateTime Date, int SuccessCount, int FailureCount);

public sealed record WeeklyBarItem(string DateLabel, double SuccessHeight, double FailureHeight);
