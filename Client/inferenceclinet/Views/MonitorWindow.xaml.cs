using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using inferenceclinet.Models;
using inferenceclinet.Services;

namespace inferenceclinet.Views;

public partial class MonitorWindow : Window
{
    private readonly ObservableCollection<InspectionRecord> _history = new();
    private readonly IRequestService _requestService;

    private CameraCaptureService? _camera;
    private CancellationTokenSource? _captureCts;

    public MonitorWindow()
    {
        InitializeComponent();
        HistoryGrid.ItemsSource = _history;
        _requestService = new RequestService(new HttpClient { BaseAddress = new Uri(AppConfig.MainServerBaseUrl) });
    }

    private void OnAnalyzeClick(object sender, RoutedEventArgs e)
    {
        if (_captureCts is not null)
        {
            return; // 이미 실행 중
        }

        try
        {
            _camera = new CameraCaptureService();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "카메라 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        CamPlaceholderText.Visibility = Visibility.Collapsed;
        _captureCts = new CancellationTokenSource();
        _ = RunCaptureLoopAsync(_captureCts.Token);
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        _captureCts?.Cancel();
        _captureCts = null;

        _camera?.Dispose();
        _camera = null;

        CamPlaceholderText.Visibility = Visibility.Visible;
    }

    // 03-continuous-capture-plan.md 3절: 같은 clientId로 여러 요청이 동시에 대기하면
    // MainServer가 응답을 뒤섞을 수 있어, 한 프레임 보내고 응답(or 타임아웃)까지 기다린 뒤 다음 프레임을 보낸다.
    private async Task RunCaptureLoopAsync(CancellationToken ct)
    {
        var frameNumber = 0;

        while (!ct.IsCancellationRequested)
        {
            var captured = _camera?.CaptureFrame();
            if (captured is null)
            {
                await DelayIgnoringCancel(ct);
                continue;
            }

            var (jpeg, preview) = captured.Value;
            CamImage.Source = preview;

            frameNumber++;
            var request = new RequestMessage
            {
                Client = AppConfig.NumericClientId,
                Filename = $"frame_{frameNumber}.jpg",
                Filelastnumber = frameNumber,
                Filelength = jpeg.Length,
                Filedata = Convert.ToBase64String(jpeg)
            };

            try
            {
                var waitTask = ClientHttpHost.WaitForMainResponseAsync(
                    AppConfig.NumericClientId.ToString(),
                    TimeSpan.FromMilliseconds(AppConfig.ResponseWaitTimeoutMs));

                await _requestService.SendAsync(request, ct);
                var response = await waitTask;

                HandleResponse(response);
            }
            catch (HttpRequestException)
            {
                DefectStatusText.Text = "서버 연결 실패";
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await DelayIgnoringCancel(ct);
        }
    }

    private void HandleResponse(MainResponsePayload? response)
    {
        if (response is null)
        {
            DefectStatusText.Text = "응답 없음(시간 초과)";
            return;
        }

        var isSuccess = string.Equals(response.SucessRate, "sucess", StringComparison.OrdinalIgnoreCase);
        DefectStatusText.Text = isSuccess ? "정상" : "불량";

        var boxText = response.Box is { Length: 4 } box
            ? $"{box[0]:0.0}, {box[1]:0.0}, {box[2]:0.0}, {box[3]:0.0}"
            : "-";

        _history.Add(new InspectionRecord(
            DateTime.Now,
            isSuccess ? "정상" : "불량",
            Confidence: response.Confidence is double confidence
                ? $"{confidence:P2}"
                : "-",
            DefectType: response.ProductName,
            Box: boxText));
    }

    private static async Task DelayIgnoringCancel(CancellationToken ct)
    {
        try
        {
            await Task.Delay(AppConfig.CaptureIntervalMs, ct);
        }
        catch (OperationCanceledException)
        {
            // 중지 버튼으로 인한 취소는 무시하고 루프 쪽에서 종료 처리
        }
    }

    private void OnHistoryRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryGrid.SelectedItem is InspectionRecord record)
        {
            new DefectDetailWindow(record).Show();
        }
    }
}
