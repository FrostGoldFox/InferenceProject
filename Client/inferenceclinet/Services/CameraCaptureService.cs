using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;

namespace inferenceclinet.Services;

public sealed class CameraCaptureService : IDisposable
{
    private readonly VideoCapture _capture;

    public CameraCaptureService(int deviceIndex = 0)
    {
        _capture = new VideoCapture(deviceIndex);
        if (!_capture.IsOpened())
        {
            _capture.Dispose();
            throw new InvalidOperationException($"카메라(index={deviceIndex})를 열 수 없습니다.");
        }
    }

    // 한 프레임을 읽어 JPEG 바이트와 미리보기용 BitmapSource를 함께 반환. 실패 시 null.
    public (byte[] Jpeg, BitmapSource Preview)? CaptureFrame(int jpegQuality = 85)
    {
        using var frame = new Mat();
        if (!_capture.Read(frame) || frame.Empty())
        {
            return null;
        }

        var jpeg = frame.ImEncode(".jpg", [(int)ImwriteFlags.JpegQuality, jpegQuality]);
        var preview = frame.ToBitmapSource();
        preview.Freeze();

        return (jpeg, preview);
    }

    public void Dispose() => _capture.Dispose();
}
