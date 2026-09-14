using System.Windows;
using inferenceclinet.Models;

namespace inferenceclinet.Views;

public partial class DefectDetailWindow : Window
{
    public DefectDetailWindow(InspectionRecord record)
    {
        InitializeComponent();

        TimeText.Text = record.Time.ToString("yyyy-MM-dd HH:mm:ss");
        DefectTypeText.Text = record.DefectType ?? "-";
        ConfidenceText.Text = record.Confidence ?? "-";
    }

    private void OnRetryClick(object sender, RoutedEventArgs e)
    {
        // TODO: 재검사 요청 (③ 관련, 스펙 미정)
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
