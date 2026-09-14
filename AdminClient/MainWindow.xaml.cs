using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AdminClientUI.Data;

namespace AdminClientUI;

public partial class MainWindow : Window
{
    private readonly RecordsRepository _recordsRepository = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        LoginPage.Visibility = Visibility.Collapsed;
        AdminShell.Visibility = Visibility.Visible;
        ShowRecords();
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        AdminShell.Visibility = Visibility.Collapsed;
        LoginPage.Visibility = Visibility.Visible;
        PasswordInput.Clear();
    }

    private void RecordsNav_Click(object sender, RoutedEventArgs e) => ShowRecords();

    private async void StatsNav_Click(object sender, RoutedEventArgs e)
    {
        RecordsPage.Visibility = Visibility.Collapsed;
        StatisticsPage.Visibility = Visibility.Visible;
        RecordsNavButton.Background = Brushes.Transparent;
        StatsNavButton.Background = new SolidColorBrush(Color.FromRgb(27, 87, 151));

        await LoadSummaryAsync();
    }

    private async void ShowRecords()
    {
        RecordsPage.Visibility = Visibility.Visible;
        StatisticsPage.Visibility = Visibility.Collapsed;
        RecordsNavButton.Background = new SolidColorBrush(Color.FromRgb(27, 87, 151));
        StatsNavButton.Background = Brushes.Transparent;

        await LoadRecordsAsync();
    }

    private async void RecordsSearch_Click(object sender, RoutedEventArgs e) => await LoadRecordsAsync();

    private async void StatsDateRange_Changed(object sender, SelectionChangedEventArgs e) => await LoadSummaryAsync();

    private async Task LoadRecordsAsync()
    {
        try
        {
            var (start, endExclusive) = GetSelectedRange(RecordsStartDatePicker, RecordsEndDatePicker);
            var records = await _recordsRepository.GetRecentRecordsAsync(start, endExclusive);
            RecordsList.ItemsSource = records;
            RecordsCountText.Text = $"총 {records.Count}건의 기록이 있습니다.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"관리 기록을 불러오지 못했습니다.\n{ex.Message}", "DB 조회 오류",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task LoadSummaryAsync()
    {
        try
        {
            var (start, endExclusive) = GetSelectedRange(StatsStartDatePicker, StatsEndDatePicker);

            var summary = await _recordsRepository.GetSummaryAsync(start, endExclusive);
            TotalCountText.Text = summary.TotalCount.ToString();
            SuccessCountText.Text = summary.SuccessCount.ToString();
            FailureCountText.Text = summary.FailureCount.ToString();
            ActiveClientCountText.Text = summary.ActiveClientCount.ToString();

            var recentResults = await _recordsRepository.GetRecentResultRatioAsync(1000);
            var successRatio = recentResults.TotalCount == 0
                ? 0.0
                : recentResults.SuccessCount / (double)recentResults.TotalCount;
            var failureRatio = recentResults.TotalCount == 0
                ? 0.0
                : recentResults.FailureCount / (double)recentResults.TotalCount;
            DonutTotalText.Text = recentResults.TotalCount.ToString();
            DonutSuccessText.Text = $"성공 {successRatio:P1}";
            DonutFailureText.Text = $"실패 {failureRatio:P1}";
            ResultArcPath.Data = BuildDonutArc(successRatio);

            var trend = await _recordsRepository.GetDailyTrendAsync(start, endExclusive.AddDays(-1));
            var trendWidth = Math.Max(725, 50 + (Math.Max(0, trend.Count - 1) * 48));
            var trendEndX = trendWidth - 25;
            TrendChartContent.Width = trendWidth;
            TrendLineBottom.X2 = trendEndX;
            TrendLineLower.X2 = trendEndX;
            TrendLineUpper.X2 = trendEndX;
            TrendLineTop.X2 = trendEndX;
            TrendPolyline.Points = BuildTrendPoints(trend, trendEndX);
            UpdateTrendDateLabels(trend, trendEndX);

            // "최근 7일 작업 현황" 카드는 이름 그대로 항상 오늘 기준 실제 최근 7일을 보여준다 — 위 기간 선택과 무관.
            var weekly = await _recordsRepository.GetLast7DaysAsync();
            WeeklyBarsControl.ItemsSource = BuildWeeklyBars(weekly);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"통계를 불러오지 못했습니다.\n{ex.Message}", "DB 조회 오류",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // DatePicker 두 개에서 (시작일, 종료일 다음날 0시)를 뽑는다. 값이 비어 있으면 오늘, 시작/종료가 뒤바뀌면 자동으로 바로잡는다.
    private static (DateTime Start, DateTime EndExclusive) GetSelectedRange(DatePicker startPicker, DatePicker endPicker)
    {
        var start = (startPicker.SelectedDate ?? DateTime.Today).Date;
        var end = (endPicker.SelectedDate ?? DateTime.Today).Date;

        if (end < start)
        {
            (start, end) = (end, start);
        }

        if ((end - start).TotalDays > 366)
        {
            throw new InvalidOperationException("기간은 최대 366일까지 조회할 수 있습니다.");
        }

        return (start, end.AddDays(1));
    }

    // 일별 요청 수 추이 Canvas 좌표: x축 25~동적 너비, y축 40(최댓값)~190(0)
    private static PointCollection BuildTrendPoints(IReadOnlyList<DailyCount> trend, double xEnd)
    {
        var points = new PointCollection();
        if (trend.Count == 0)
        {
            return points;
        }

        const double xStart = 25, yTop = 40, yBottom = 190;
        var maxValue = Math.Max(1, trend.Max(t => t.Total));
        var stepX = trend.Count > 1 ? (xEnd - xStart) / (trend.Count - 1) : 0;

        for (var i = 0; i < trend.Count; i++)
        {
            var x = xStart + stepX * i;
            var ratio = trend[i].Total / (double)maxValue;
            var y = yBottom - ratio * (yBottom - yTop);
            points.Add(new Point(x, y));
        }

        return points;
    }

    private void UpdateTrendDateLabels(IReadOnlyList<DailyCount> trend, double xEnd)
    {
        TrendDateLabelsCanvas.Children.Clear();
        if (trend.Count == 0)
        {
            return;
        }

        const double xStart = 25;
        const double labelWidth = 44;

        for (var dataIndex = 0; dataIndex < trend.Count; dataIndex++)
        {
            var x = trend.Count == 1
                ? xStart
                : xStart + ((xEnd - xStart) * dataIndex / (trend.Count - 1d));

            var label = new TextBlock
            {
                Text = trend[dataIndex].Date.ToString("MM/dd"),
                Width = labelWidth,
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                Foreground = (Brush)FindResource("MutedBrush")
            };

            Canvas.SetLeft(label, x - (labelWidth / 2));
            TrendDateLabelsCanvas.Children.Add(label);
        }
    }
    // 처리 결과 비율 도넛: 중심(85,85), 반지름 73인 원 위에서 12시 방향부터 시계방향으로 successRatio*360도 호를 그린다.
    private static Geometry BuildDonutArc(double successRatio)
    {
        const double cx = 85, cy = 85, r = 73;
        var ratio = Math.Clamp(successRatio, 0.0, 1.0);
        if (ratio <= 0.0001)
        {
            return Geometry.Empty;
        }

        var angleDegrees = Math.Min(ratio * 360.0, 359.999); // 360도 완전한 원은 Arc 하나로 표현 불가
        var angleRadians = angleDegrees * Math.PI / 180.0;

        var startPoint = new Point(cx, cy - r);
        var endPoint = new Point(cx + r * Math.Sin(angleRadians), cy - r * Math.Cos(angleRadians));
        var isLargeArc = angleDegrees > 180.0;

        var figure = new PathFigure { StartPoint = startPoint };
        figure.Segments.Add(new ArcSegment(endPoint, new Size(r, r), 0, isLargeArc, SweepDirection.Clockwise, isStroked: true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    // 최근 7일 막대: 7일 중 (성공+실패) 최댓값을 막대 최대 높이(150)에 맞춰 두 값을 비례 배분한다.
    private static List<WeeklyBarItem> BuildWeeklyBars(IReadOnlyList<DailyBreakdown> days)
    {
        const double maxBarHeight = 150;
        var maxTotal = Math.Max(1, days.Count == 0 ? 1 : days.Max(d => d.SuccessCount + d.FailureCount));

        return days
            .Select(d => new WeeklyBarItem(
                d.Date.ToString("MM'/'dd"),
                Math.Round(d.SuccessCount / (double)maxTotal * maxBarHeight, 1),
                Math.Round(d.FailureCount / (double)maxTotal * maxBarHeight, 1)))
            .ToList();
    }
}
