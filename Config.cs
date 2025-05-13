public class Config
{
    public required string DiscordToken { get; set; }
    public required string GoogleKey { get; set; }
    public required string CalendarId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong ServerId { get; set; }
    public int ThreadCreateTime { get; set; }
    public int EventCreateTime { get; set; }
    public double RefreshTimer { get; set; }
    public double CalendarRefresh { get; set; }
    public bool DebugLogging { get; set; }
    public bool ResetOnRestart { get; set; }
    public List<string> TaggedUsers { get; set; }
    public List<ulong> AdminId { get; set; }
    public int MaxDays { get; set; }
    public bool DebugMode { get; set; }
    public ulong BotAdminID { get; set; } 
}