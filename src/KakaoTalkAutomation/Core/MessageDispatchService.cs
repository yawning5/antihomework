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
        var polledAt = DateTimeOffset.Now;
        var next = await _repository.GetNextAsync(settings, cancellationToken);
        if (next is null)
        {
            return new DispatchResult
            {
                Outcome = DispatchOutcome.NoWork,
                PolledAt = polledAt,
                Detail = "No pending message."
            };
        }

        var sendResult = await Task.Run(() => MessageSender.Send(next.RoomName, next.Message), cancellationToken);
        var sequenceCompletedAt = DateTimeOffset.Now;
        var duration = sequenceCompletedAt - polledAt;
        var confirmed = await _confirmationPolicy.ConfirmAsync(next, sendResult, cancellationToken);

        if (!confirmed)
        {
            var result = new DispatchResult
            {
                Outcome = DispatchOutcome.SendFailed,
                Message = next,
                PolledAt = polledAt,
                SequenceCompletedAt = sequenceCompletedAt,
                DurationMs = duration.TotalMilliseconds,
                DurationSec = duration.TotalSeconds,
                Detail = $"Send sequence failed for msg_id={next.MsgId}."
            };
            await LogResultAsync(result, cancellationToken);
            return result;
        }

        try
        {
            await _repository.DeleteAsync(settings, next.MsgId, cancellationToken);
        }
        catch (Exception ex)
        {
            var result = new DispatchResult
            {
                Outcome = DispatchOutcome.DeleteFailed,
                Message = next,
                PolledAt = polledAt,
                SequenceCompletedAt = sequenceCompletedAt,
                DurationMs = duration.TotalMilliseconds,
                DurationSec = duration.TotalSeconds,
                Detail = $"Delete failed after send for msg_id={next.MsgId}: {ex.Message}"
            };
            await LogResultAsync(result, cancellationToken);
            return result;
        }

        if (postSendDelayMs > 0)
            await Task.Delay(postSendDelayMs, cancellationToken);

        var successResult = new DispatchResult
        {
            Outcome = DispatchOutcome.SentAndDeleted,
            Message = next,
            PolledAt = polledAt,
            SequenceCompletedAt = sequenceCompletedAt,
            DurationMs = duration.TotalMilliseconds,
            DurationSec = duration.TotalSeconds,
            Detail = $"Sent and deleted msg_id={next.MsgId}."
        };
        await LogResultAsync(successResult, cancellationToken);
        return successResult;
    }

    private static Task LogResultAsync(DispatchResult result, CancellationToken cancellationToken)
    {
        return DispatchLogStore.AppendAsync(new DispatchLogEntry
        {
            MsgId = result.Message?.MsgId,
            RoomName = result.Message?.RoomName ?? string.Empty,
            PolledAt = result.PolledAt ?? DateTimeOffset.Now,
            SequenceCompletedAt = result.SequenceCompletedAt,
            DurationMs = result.DurationMs,
            DurationSec = result.DurationSec,
            Result = result.Outcome.ToString(),
            Detail = result.Detail
        }, cancellationToken);
    }
}
