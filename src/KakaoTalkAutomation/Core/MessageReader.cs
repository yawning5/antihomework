using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using KakaoTalkAutomation.Helpers;
using Microsoft.Extensions.Logging;

namespace KakaoTalkAutomation.Core;

/// <summary>
/// 카카오톡 채팅방에서 메시지를 읽어오는 클래스
///
/// PrintWindow(비활성 캡처) + Windows OCR을 결합하여
/// 카카오톡 창에 포커스를 주지 않고 백그라운드에서 메시지를 읽습니다.
///
/// ※ 이 방식은 화면을 빼앗지 않으므로 사용자가 PC를 쓰는 동안에도
///   방해 없이 모니터링이 가능합니다.
/// </summary>
public class MessageReader : IDisposable
{
    private readonly ILogger<MessageReader> _logger;
    private readonly UIA3Automation _automation;

    /// <summary>
    /// 채팅방별 마지막으로 읽은 메시지 해시를 추적합니다.
    /// </summary>
    private readonly Dictionary<IntPtr, HashSet<string>> _knownMessageHashes = new();

    public MessageReader(ILogger<MessageReader> logger)
    {
        _logger = logger;
        _automation = new UIA3Automation();
    }

    /// <summary>
    /// 채팅방 창의 UI 구조를 진단하여 콘솔에 출력합니다.
    /// </summary>
    public void DiagnoseUIStructure(IntPtr chatRoomHandle)
    {
        try
        {
            var window = _automation.FromHandle(chatRoomHandle);
            if (window == null)
            {
                Console.WriteLine("  [오류] 창 요소를 가져올 수 없습니다.");
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== 카카오톡 채팅방 UI 구조 진단 ===\n");
            Console.ResetColor();

            // 1단계: 직계 자식 요소
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  [1단계] 직계 자식 요소:");
            Console.ResetColor();

            var children = window.FindAllChildren();
            for (int i = 0; i < children.Length; i++)
            {
                PrintElementInfo(children[i], $"    [{i}]");
            }

            // 2단계: Win32 자식 창 열거
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  [2단계] Win32 자식 창:");
            Console.ResetColor();

            int childIndex = 0;
            Win32Api.EnumChildWindows(chatRoomHandle, (hWnd, lParam) =>
            {
                var className = Win32Api.GetWindowClassName(hWnd);
                var title = Win32Api.GetWindowTitle(hWnd);
                Console.WriteLine($"    [{childIndex++}] Class=\"{className}\" | 핸들=0x{hWnd:X} | Title=\"{title}\"");
                return true;
            }, IntPtr.Zero);

            // 3단계: OCR 텍스트 인식 테스트
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n  [3단계] OCR 텍스트 인식 테스트:");
            Console.ResetColor();

            using var bmp = CaptureHelper.CaptureWindow(chatRoomHandle);
            if (bmp != null)
            {
                var text = OcrHelper.RecognizeTextAsync(bmp).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(text))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"    OCR 성공! ({text.Length}자 인식)");
                    Console.ResetColor();

                    var lines = text.Split('\n');
                    var showCount = Math.Min(lines.Length, 15);
                    Console.WriteLine($"    총 {lines.Length}줄 (마지막 {showCount}줄 표시):");
                    for (int i = Math.Max(0, lines.Length - showCount); i < lines.Length; i++)
                    {
                        var line = lines[i].TrimEnd('\r');
                        var trimmed = line.Length > 80 ? line[..80] + "..." : line;
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"      {trimmed}");
                    }
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("    OCR 텍스트 인식 실패");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("    캡처 실패");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== 진단 완료 ===\n");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  진단 중 오류: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// 채팅방에서 모든 메시지를 읽어옵니다.
    /// PrintWindow(비활성 캡처) + Windows OCR 방식을 사용합니다.
    /// 화면 제어권을 뺏지 않습니다.
    /// </summary>
    public List<ParsedMessage> ReadMessages(IntPtr chatRoomHandle)
    {
        var messages = new List<ParsedMessage>();

        try
        {
            // 1. 비활성 캡처 (포커스 뺏지 않음)
            using var bmp = CaptureHelper.CaptureWindow(chatRoomHandle);
            if (bmp == null)
            {
                _logger.LogWarning("채팅방 캡처 실패");
                return messages;
            }

            // 2. OCR로 텍스트 인식
            var ocrText = OcrHelper.RecognizeTextAsync(bmp).GetAwaiter().GetResult();
            if (string.IsNullOrEmpty(ocrText))
            {
                _logger.LogWarning("OCR 텍스트 인식 실패");
                return messages;
            }

            // 3. OCR 텍스트 파싱
            messages = ParseOcrText(ocrText);
            _logger.LogDebug("OCR 메시지 {Count}개 파싱 완료", messages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "메시지 읽기 중 오류 발생");
        }

        return messages;
    }

    /// <summary>
    /// 새로 수신된 메시지만 반환합니다.
    /// </summary>
    public List<ParsedMessage> ReadNewMessages(IntPtr chatRoomHandle)
    {
        var allMessages = ReadMessages(chatRoomHandle);
        var newMessages = new List<ParsedMessage>();

        if (!_knownMessageHashes.TryGetValue(chatRoomHandle, out var knownHashes))
        {
            knownHashes = new HashSet<string>();
            _knownMessageHashes[chatRoomHandle] = knownHashes;
        }

        foreach (var msg in allMessages)
        {
            var hash = $"{msg.Sender}|{msg.Content}|{msg.Timestamp:HHmm}";
            if (knownHashes.Add(hash))
            {
                newMessages.Add(msg);
            }
        }

        if (newMessages.Count > 0)
        {
            _logger.LogInformation("새 메시지 {Count}개 감지됨", newMessages.Count);
        }

        // 해시 세트가 너무 커지면 정리
        if (knownHashes.Count > 5000)
        {
            _knownMessageHashes[chatRoomHandle] = new HashSet<string>(
                knownHashes.Skip(knownHashes.Count - 3000));
        }

        return newMessages;
    }

    /// <summary>
    /// 기존 메시지를 모두 "이미 읽음" 처리합니다.
    /// 모니터링 시작 시 호출합니다.
    /// </summary>
    public void InitializeMessageCount(IntPtr chatRoomHandle)
    {
        var allMessages = ReadMessages(chatRoomHandle);
        var hashes = new HashSet<string>();

        foreach (var msg in allMessages)
        {
            var hash = $"{msg.Sender}|{msg.Content}|{msg.Timestamp:HHmm}";
            hashes.Add(hash);
        }

        _knownMessageHashes[chatRoomHandle] = hashes;
        _logger.LogInformation("채팅방 메시지 초기화: {Count}개 (이미 읽음 처리)", allMessages.Count);
    }

    // ========================================
    // OCR 텍스트 파싱
    // ========================================

    /// <summary>
    /// OCR로 인식된 텍스트를 파싱하여 메시지 목록을 생성합니다.
    ///
    /// 카카오톡 채팅방 화면의 OCR 텍스트 특성:
    ///   - 보낸 사람 이름이 짧은 한 줄로 나타남
    ///   - 메시지 내용이 그 다음 줄에 나타남
    ///   - 시간이 "오전/오후 H:MM" 형태로 나타남
    ///   - 날짜 구분선이 "YYYY년 M월 D일 요일" 형태로 나타남
    /// </summary>
    private List<ParsedMessage> ParseOcrText(string text)
    {
        var messages = new List<ParsedMessage>();
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r').Trim()).ToArray();

        string? currentSender = null;
        var contentLines = new List<string>();
        DateTime lastTime = DateTime.Now;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            // 날짜 구분선 건너뛰기
            if (IsDateSeparator(line)) continue;

            // 시간 패턴 확인
            if (IsTimePattern(line))
            {
                lastTime = ParseKakaoTime(line);

                // 시간을 만나면 이전 메시지 저장
                if (contentLines.Count > 0 && currentSender != null)
                {
                    messages.Add(new ParsedMessage
                    {
                        Sender = currentSender,
                        Content = string.Join(" ", contentLines),
                        Timestamp = lastTime
                    });
                    contentLines.Clear();
                }
                continue;
            }

            // 보낸 사람 이름 판별 (짧은 텍스트, 다음 줄이 메시지)
            if (IsSenderName(line, lines, i))
            {
                // 이전 메시지 저장
                if (contentLines.Count > 0 && currentSender != null)
                {
                    messages.Add(new ParsedMessage
                    {
                        Sender = currentSender,
                        Content = string.Join(" ", contentLines),
                        Timestamp = lastTime
                    });
                    contentLines.Clear();
                }

                currentSender = line;
                continue;
            }

            // 메시지 내용
            if (currentSender == null)
            {
                currentSender = "알 수 없음";
            }
            contentLines.Add(line);
        }

        // 마지막 메시지 저장
        if (contentLines.Count > 0 && currentSender != null)
        {
            messages.Add(new ParsedMessage
            {
                Sender = currentSender,
                Content = string.Join(" ", contentLines),
                Timestamp = lastTime
            });
        }

        return messages;
    }

    /// <summary>
    /// 텍스트가 시간 패턴인지 확인합니다.
    /// </summary>
    private static bool IsTimePattern(string text)
    {
        text = text.Trim();

        // "오전/오후 H:MM" 패턴
        if (Regex.IsMatch(text, @"^(오전|오후)\s*\d{1,2}:\d{2}$"))
            return true;

        return false;
    }

    /// <summary>
    /// 날짜 구분선 여부를 확인합니다.
    /// </summary>
    private static bool IsDateSeparator(string line)
    {
        // "YYYY년 M월 D일 요일" 패턴
        if (Regex.IsMatch(line, @"\d{4}년\s*\d{1,2}월\s*\d{1,2}일"))
            return true;

        // "----- ... -----" 패턴
        if (line.Count(c => c == '-') >= 4)
            return true;

        return false;
    }

    /// <summary>
    /// 텍스트가 보낸 사람 이름인지 휴리스틱으로 판단합니다.
    /// </summary>
    private static bool IsSenderName(string text, string[] allLines, int currentIndex)
    {
        // 너무 길면 이름이 아님 (이름은 보통 10자 이내)
        if (text.Length > 15) return false;

        // 줄바꿈 포함이면 이름이 아님
        if (text.Contains('\n') || text.Contains('\r')) return false;

        // 공백이 3개 이상이면 이름이 아님
        if (text.Count(c => c == ' ') > 3) return false;

        // 숫자만이면 이름이 아님 (읽지 않은 수 등)
        if (text.All(c => char.IsDigit(c) || c == ',' || c == '.')) return false;

        // 시간 패턴이면 이름이 아님
        if (IsTimePattern(text)) return false;

        // 다음 줄이 있고, 다음 줄이 시간이나 다른 내용이면 이름일 가능성 높음
        if (currentIndex + 1 < allLines.Length)
        {
            var nextLine = allLines[currentIndex + 1].Trim();
            // 다음 줄이 비어있지 않고 시간 패턴이 아니면 이것은 이름 + 메시지일 수 있음
            if (!string.IsNullOrWhiteSpace(nextLine) && !IsTimePattern(nextLine) && nextLine.Length > text.Length)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 카카오톡 시간 형식을 DateTime으로 변환합니다.
    /// </summary>
    private static DateTime ParseKakaoTime(string timeText)
    {
        try
        {
            timeText = timeText.Trim();
            var match = Regex.Match(timeText, @"(오전|오후)\s*(\d{1,2}):(\d{2})");
            if (match.Success)
            {
                var ampm = match.Groups[1].Value;
                int hour = int.Parse(match.Groups[2].Value);
                int minute = int.Parse(match.Groups[3].Value);

                if (ampm == "오후" && hour != 12) hour += 12;
                else if (ampm == "오전" && hour == 12) hour = 0;

                return DateTime.Today.AddHours(hour).AddMinutes(minute);
            }
        }
        catch { }
        return DateTime.Now;
    }

    /// <summary>
    /// UI 요소 정보를 콘솔에 출력합니다 (진단용)
    /// </summary>
    private static void PrintElementInfo(AutomationElement el, string indent)
    {
        try
        {
            var name = el.Name ?? "(없음)";
            var trimmedName = name.Length > 50 ? name[..50] + "..." : name;
            var className = el.ClassName ?? "(없음)";
            var controlType = el.ControlType;
            var automationId = el.AutomationId ?? "";

            Console.Write($"{indent} ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{controlType}");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($" | Class=\"{className}\"");

            if (!string.IsNullOrEmpty(automationId))
                Console.Write($" | Id=\"{automationId}\"");

            if (!string.IsNullOrEmpty(name) && name != "(없음)")
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($" | Name=\"{trimmedName}\"");
            }

            Console.ResetColor();
            Console.WriteLine();
        }
        catch
        {
            Console.WriteLine($"{indent} [요소 정보 읽기 실패]");
        }
    }

    public void Dispose()
    {
        _automation?.Dispose();
    }
}

/// <summary>
/// 파싱된 메시지 데이터 모델
/// </summary>
public class ParsedMessage
{
    /// <summary>보낸 사람</summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>메시지 내용</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>메시지 시간</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
