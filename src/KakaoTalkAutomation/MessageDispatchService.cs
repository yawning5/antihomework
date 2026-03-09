namespace KakaoTalkAutomation;

public sealed class MessageDispatchService
{
    private readonly ChatOutRepository _repository;
    private readonly ISendConfirmationPolicy _confirmationPolicy;

    public MessageDispatchService(ChatOutRepository repository, ISendConfirmationPolicy confirmationPolicy)
    {
        _repository = repository;
        _confirmationPolicy = confirmationPolicy;
    }

    public async Task<DispatchResult> DispatchNextAsync(
        PostgresSettings settings,
        int postSendDelayMs,
        CancellationToken cancellationToken = default)
    {
        var next = await _repository.GetNextAsync(settings, cancellationToken);
        if (next is null)
        {
            return new DispatchResult
            {
                Outcome = DispatchOutcome.NoWork,
                Detail = "No pending message."
            };
        }

        var sendResult = await Task.Run(() => MessageSender.Send(next.RoomName, next.Message), cancellationToken);
        var confirmed = await _confirmationPolicy.ConfirmAsync(next, sendResult, cancellationToken);

        if (!confirmed)
        {
            return new DispatchResult
            {
                Outcome = DispatchOutcome.SendFailed,
                Message = next,
                Detail = $"Send sequence failed for msg_id={next.MsgId}."
            };
        }

        try
        {
            await _repository.DeleteAsync(settings, next.MsgId, cancellationToken);
        }
        catch (Exception ex)
        {
            return new DispatchResult
            {
                Outcome = DispatchOutcome.DeleteFailed,
                Message = next,
                Detail = $"Delete failed after send for msg_id={next.MsgId}: {ex.Message}"
            };
        }

        if (postSendDelayMs > 0)
            await Task.Delay(postSendDelayMs, cancellationToken);

        return new DispatchResult
        {
            Outcome = DispatchOutcome.SentAndDeleted,
            Message = next,
            Detail = $"Sent and deleted msg_id={next.MsgId}."
        };
    }
}
