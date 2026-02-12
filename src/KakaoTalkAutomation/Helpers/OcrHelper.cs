using System.Drawing;
using System.Drawing.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace KakaoTalkAutomation.Helpers;

/// <summary>
/// Windows 내장 OCR(Windows.Media.Ocr)을 사용한 텍스트 인식 도우미
///
/// Windows 10/11에 기본 내장된 OCR 엔진을 사용하므로
/// 별도의 라이브러리나 학습 데이터 다운로드가 필요 없습니다.
/// 한국어 Windows에서는 한국어 OCR이 자동으로 지원됩니다.
/// </summary>
public static class OcrHelper
{
    private static OcrEngine? _ocrEngine;
    private static bool _initialized;

    /// <summary>
    /// OCR 엔진을 초기화합니다. (최초 1회만 실행)
    /// </summary>
    private static void EnsureInitialized()
    {
        if (_initialized) return;

        // 한국어 OCR 엔진 시도
        _ocrEngine = OcrEngine.TryCreateFromLanguage(
            new Windows.Globalization.Language("ko"));

        if (_ocrEngine == null)
        {
            // 한국어 실패 시 사용자 프로필 언어로 시도
            _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        }

        if (_ocrEngine == null)
        {
            ConsoleHelper.PrintWarning("OCR 엔진 초기화 실패. Windows 설정에서 한국어 언어 팩이 설치되어 있는지 확인하세요.");
        }
        else
        {
            ConsoleHelper.PrintSuccess($"OCR 엔진 초기화 완료 (언어: {_ocrEngine.RecognizerLanguage.DisplayName})");
        }

        _initialized = true;
    }

    /// <summary>
    /// Bitmap 이미지에서 텍스트를 인식합니다.
    /// </summary>
    /// <param name="bitmap">인식할 이미지</param>
    /// <returns>인식된 텍스트 (실패 시 null)</returns>
    public static async Task<string?> RecognizeTextAsync(Bitmap bitmap)
    {
        EnsureInitialized();

        if (_ocrEngine == null) return null;

        // System.Drawing.Bitmap → 임시 파일 → WinRT SoftwareBitmap 변환
        var tempPath = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid()}.png");

        try
        {
            // 임시 파일로 저장
            bitmap.Save(tempPath, ImageFormat.Png);

            // WinRT StorageFile로 로드
            var file = await StorageFile.GetFileFromPathAsync(tempPath);
            using var stream = await file.OpenAsync(FileAccessMode.Read);

            // BitmapDecoder → SoftwareBitmap
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            // OCR 실행
            var result = await _ocrEngine.RecognizeAsync(softwareBitmap);

            return result.Text;
        }
        catch (Exception ex)
        {
            ConsoleHelper.PrintError($"OCR 처리 중 오류: {ex.Message}");
            return null;
        }
        finally
        {
            // 임시 파일 정리
            try { File.Delete(tempPath); } catch { }
        }
    }

    /// <summary>
    /// OCR 결과를 줄 단위로 반환합니다.
    /// Windows OCR은 줄(Line) 단위로 결과를 제공하여 더 정확한 파싱이 가능합니다.
    /// </summary>
    /// <param name="bitmap">인식할 이미지</param>
    /// <returns>줄 단위 텍스트 목록 (실패 시 null)</returns>
    public static async Task<List<OcrLine>?> RecognizeLinesAsync(Bitmap bitmap)
    {
        EnsureInitialized();

        if (_ocrEngine == null) return null;

        var tempPath = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid()}.png");

        try
        {
            bitmap.Save(tempPath, ImageFormat.Png);

            var file = await StorageFile.GetFileFromPathAsync(tempPath);
            using var stream = await file.OpenAsync(FileAccessMode.Read);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            var result = await _ocrEngine.RecognizeAsync(softwareBitmap);

            var lines = new List<OcrLine>();
            foreach (var line in result.Lines)
            {
                // 각 줄의 바운딩 박스(위치 정보)도 함께 저장
                double avgY = 0;
                double avgX = 0;
                if (line.Words.Count > 0)
                {
                    avgX = line.Words.Average(w => w.BoundingRect.X);
                    avgY = line.Words.Average(w => w.BoundingRect.Y);
                }

                lines.Add(new OcrLine
                {
                    Text = line.Text,
                    X = avgX,
                    Y = avgY
                });
            }

            return lines;
        }
        catch (Exception ex)
        {
            ConsoleHelper.PrintError($"OCR 줄 인식 중 오류: {ex.Message}");
            return null;
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    /// <summary>
    /// OCR 엔진이 사용 가능한지 확인합니다.
    /// </summary>
    public static bool IsAvailable()
    {
        EnsureInitialized();
        return _ocrEngine != null;
    }
}

/// <summary>
/// OCR로 인식된 한 줄의 텍스트와 위치 정보
/// </summary>
public class OcrLine
{
    /// <summary>인식된 텍스트</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>텍스트의 X 좌표 (왼쪽으로부터)</summary>
    public double X { get; set; }

    /// <summary>텍스트의 Y 좌표 (위쪽으로부터)</summary>
    public double Y { get; set; }
}
