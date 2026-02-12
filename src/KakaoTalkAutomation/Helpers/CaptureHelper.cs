using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KakaoTalkAutomation.Helpers;

/// <summary>
/// 화면 캡처 관련 도우미 클래스
/// 비활성 창 캡처(PrintWindow) 기능을 제공합니다.
/// </summary>
public static class CaptureHelper
{
    /// <summary>
    /// 지정된 창 핸들의 화면을 캡처하여 Bitmap으로 반환합니다.
    /// PrintWindow API를 사용하여 가려진 창도 캡처할 수 있습니다.
    /// </summary>
    /// <param name="hWnd">캡처할 창의 핸들</param>
    /// <returns>캡처된 Bitmap 이미지 (실패 시 null)</returns>
    public static Bitmap? CaptureWindow(IntPtr hWnd)
    {
        // 창 크기 가져오기
        if (!Win32Api.GetWindowRect(hWnd, out var rect))
        {
            return null;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0) return null;

        // 비트맵 생성
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var gfx = Graphics.FromImage(bmp))
        {
            var hdcBitmap = gfx.GetHdc();

            try
            {
                // PrintWindow 시도 (비활성 캡처 핵심)
                // PW_CLIENTONLY (1) = 클라이언트 영역만 캡처 (테두리 제외) -> 0 = 전체 캡처
                bool result = Win32Api.PrintWindow(hWnd, hdcBitmap, 0);
                
                if (!result)
                {
                    // PrintWindow 실패 시 BitBlt 시도 (화면에 보여야 함)
                    var hdcWindow = Win32Api.GetWindowDC(hWnd);
                    Win32Api.BitBlt(hdcBitmap, 0, 0, width, height, hdcWindow, 0, 0, Win32Api.SRCCOPY);
                    Win32Api.ReleaseDC(hWnd, hdcWindow);
                }
            }
            finally
            {
                gfx.ReleaseHdc(hdcBitmap);
            }
        }

        return bmp;
    }

    /// <summary>
    /// 캡처된 이미지를 파일로 저장합니다.
    /// </summary>
    public static string SaveCapture(Bitmap bmp, string prefix = "capture")
    {
        try
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "captures");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var filename = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var path = Path.Combine(folder, filename);

            bmp.Save(path, ImageFormat.Png);
            return path;
        }
        catch (Exception ex)
        {
            ConsoleHelper.PrintError($"이미지 저장 실패: {ex.Message}");
            return string.Empty;
        }
    }
}
