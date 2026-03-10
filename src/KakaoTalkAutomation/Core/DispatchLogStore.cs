using System.Text.Json;

namespace KakaoTalkAutomation;

public static class DispatchLogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static string LogPath =>
        Path.Combine(AppContext.BaseDirectory, "dispatch-log.jsonl");

    public static async Task AppendAsync(DispatchLogEntry entry, CancellationToken cancellationToken = default)
    {
        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;

        await Gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(LogPath, line, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }
}
