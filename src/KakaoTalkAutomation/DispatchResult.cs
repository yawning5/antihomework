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
    public string Detail { get; init; } = string.Empty;
}
