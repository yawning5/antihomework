namespace KakaoTalkAutomation;

public sealed class ClientSettings
{
    public PostgresSettings Postgres { get; set; } = new();
    public string DefaultQuery { get; set; } = "select now() as server_time;";
}

public sealed class PostgresSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string SearchPath { get; set; } = "";
    public bool SslModeRequire { get; set; }
}
