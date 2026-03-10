using System.Text.Json;

namespace KakaoTalkAutomation;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string SettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "client-settings.json");

    public static ClientSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new ClientSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<ClientSettings>(json, JsonOptions) ?? new ClientSettings();
        }
        catch
        {
            return new ClientSettings();
        }
    }

    public static void Save(ClientSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
