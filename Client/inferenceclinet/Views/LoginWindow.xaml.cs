using System.Net.Http;
using System.Windows;
using inferenceclinet.Services;

namespace inferenceclinet.Views;

public partial class LoginWindow : Window
{
    private static readonly TimeSpan LoginResponseTimeout = TimeSpan.FromSeconds(10);

    private readonly ILoginService _loginService;

    public LoginWindow()
    {
        InitializeComponent();
        _loginService = new LoginService(new HttpClient { BaseAddress = new Uri(AppConfig.MiddlewareBaseUrl) });
        _ = ClientHttpHost.EnsureStartedAsync(AppConfig.ClientListenUrl);
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        LoginButton.IsEnabled = false;

        try
        {
            var id = IdTextBox.Text;
            var localHash = PasswordHasher.Hash(LoginPasswordBox.Password);

            // Middleware가 push할 loginResponse를 먼저 기다리기 시작한 뒤 요청을 보낸다 (순서 역전 시 응답을 놓칠 수 있음).
            var waitTask = ClientHttpHost.WaitForLoginResponseAsync(id, LoginResponseTimeout);
            await _loginService.LoginAsync(new LoginMessage { Id = id, HashPassword = localHash });

            var storedHash = await waitTask;

            if (storedHash is null)
            {
                ErrorText.Text = "서버 응답이 없습니다 (시간 초과).";
                return;
            }

            if (!string.Equals(storedHash, localHash, StringComparison.Ordinal))
            {
                ErrorText.Text = "아이디 또는 비밀번호가 올바르지 않습니다.";
                return;
            }

            SessionContext.ClientNo = id;

            new MonitorWindow().Show();
            Close();
        }
        catch (HttpRequestException ex)
        {
            ErrorText.Text = $"서버에 연결할 수 없습니다: {ex.Message}";
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }
}
