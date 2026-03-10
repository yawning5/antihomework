namespace KakaoTalkAutomation;

public sealed class ChatOutMessage
{
    public long MsgId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
