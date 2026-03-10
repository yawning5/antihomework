namespace KakaoTalkAutomation;

public sealed class DispatchLogEntry
{
    public long? MsgId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public DateTimeOffset PolledAt { get; init; }
    public DateTimeOffset? SequenceCompletedAt { get; init; }
    public double? DurationMs { get; init; }
    public double? DurationSec { get; init; }
    public string Result { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}
