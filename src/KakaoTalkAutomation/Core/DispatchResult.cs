namespace KakaoTalkAutomation;

public enum DispatchOutcome
{
    NoWork,
    SentAndDeleted,
    SendFailed,
    DeleteFailed
}

public sealed class DispatchResult
{
    public DispatchOutcome Outcome { get; init; }
    public ChatOutMessage? Message { get; init; }
    public DateTimeOffset? PolledAt { get; init; }
    public DateTimeOffset? SequenceCompletedAt { get; init; }
    public double? DurationMs { get; init; }
    public double? DurationSec { get; init; }
    public string Detail { get; init; } = string.Empty;
}
