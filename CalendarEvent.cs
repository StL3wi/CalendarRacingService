public class CalendarEvent
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public DateTime StartTime { get; set; }
    public string Location { get; set; }
    public ulong ServerID { get; set; }
    public ulong ChannelID { get; set; }
    public bool DiscordEventCreated { get; set; } = false;
    public bool ThreadCreated { get; set; } = false;
    public ulong DiscordEventId { get; set; } = 0;
    public ulong DiscordThreadId { get; set; } = 0;
    public List<ulong> IntresetedUsers { get; set; } = new List<ulong>();
    public int ArchiveTime { get; set; } = 0; // Time in minutes to archive the thread

    // Added timestamp for last update
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    // Method to update the LastUpdated timestamp
    public void UpdateTimestamp()
    {
        LastUpdated = DateTime.Now;
    }
}