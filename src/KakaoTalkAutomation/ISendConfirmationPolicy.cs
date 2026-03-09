namespace KakaoTalkAutomation;

public interface ISendConfirmationPolicy
{
    Task<bool> ConfirmAsync(ChatOutMessage message, bool sendResult, CancellationToken cancellationToken = default);
}
