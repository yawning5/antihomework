namespace KakaoTalkAutomation;

public sealed class SequenceCompletedConfirmationPolicy : ISendConfirmationPolicy
{
    public Task<bool> ConfirmAsync(ChatOutMessage message, bool sendResult, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(sendResult);
    }
}
