using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Google.Apis.Calendar.v3.Data;
using System.Threading.Channels;

public class DiscordService
{
    private static DiscordSocketClient _client;
    private readonly Config _config;
    public DiscordSocketClient Client => _client;
    private Dictionary<string, CalendarEvent> _calendarEvents;
    public static EventManager _eventManager;
    public static JsonSaver _jsonSaver;
    private static SocketGuild _guild;
    private static SocketTextChannel _channel;
    public DiscordService(Config config)
    {
        _config = config;
        _calendarEvents = new Dictionary<string, CalendarEvent>();

        var discordConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent |
                             GatewayIntents.GuildMembers | GatewayIntents.DirectMessages | GatewayIntents.GuildScheduledEvents,
            LogLevel = LogSeverity.Info,

        };
        if (config.DebugMode)
        {
            discordConfig.LogLevel = LogSeverity.Debug;
        }

        _client = new DiscordSocketClient(discordConfig);
    }

    private bool _isInitialized = false;

    // Check if the bot is ready
    private bool _isReady = false; // Flag to track readiness

    // Start the Discord client
    public async Task StartClientAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;


        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += MessageReceivedAsync;
        _client.GuildScheduledEventStarted += GuildScheduledEventStartedAsync;
        _client.GuildScheduledEventCompleted += GuildScheduledEventCompletedAsync;
        _client.GuildScheduledEventCancelled += GuildScheduledEventCancelledAsync;

        await _client.LoginAsync(TokenType.Bot, _config.DiscordToken);
        await _client.StartAsync();
    }

    // Log messages
    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }

    // Notify when the bot is ready
    private async Task ReadyAsync()
    {
        Console.WriteLine("Bot is connected and ready.");
        _isReady = true;

        foreach (var guild in _client.Guilds)
        {
            Console.WriteLine($"Bot is in: {guild.Name} (ID: {guild.Id})");
        }

        // Call initialization logic or notify readiness
        await Task.CompletedTask;
    }

    // Handle messages received
    private async Task MessageReceivedAsync(SocketMessage message)
    {
        // Handle messages in threads
        if (message.Channel is SocketThreadChannel threadChannel)
        {
            if (message.Author.IsBot) return; // Ignore bot messages
            if (_config.AdminId.Contains(message.Author.Id) && message.Content.StartsWith("!"))
            {
                await HandleAdminCommands(message);
            }
            Console.WriteLine($"Message in thread: {message.Content}");
        }
        // Handle DM messages
        else if (message.Channel is SocketDMChannel dmChannel)
        {
            await HandleDMAdminCommands(message);
        }
        await Task.CompletedTask;
    }

    // Handle (DM) Admin Commands
    private async Task HandleDMAdminCommands(SocketMessage message)
    {

    }

    // Handle (Discord thread) Admin commands
    private async Task HandleAdminCommands(SocketMessage message)
    {
        // Get the command without the prefix
        string commandWithArgs = message.Content.Substring(1).Trim();

        // Split into command and arguments
        string[] parts = commandWithArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string command = parts[0].ToLower(); // Just the command part

        // Get the event for this thread
        string stringCalEvent = _eventManager.GetEventIdByDiscordThreadId(message.Channel.Id);
        if (stringCalEvent == null)
        {
            Console.WriteLine($"Could not find event with thread ID: {message.Channel.Id}");
            return;
        }

        CalendarEvent calEvent = _eventManager.GetEvent(stringCalEvent);

        switch (command)
        {
            // End the event
            case "end":
                await EndEvent(calEvent);
                break;

            // Close the thread
            case "close":
                await CloseThread(calEvent);
                break;

            // Add time before archive
            case "addtime":
                int minutesToAdd = 1440; // Default to 1 day (1440 minutes)

                // Check if a number was provided with the command
                if (parts.Length >= 2 && int.TryParse(parts[1], out int parsedMinutes))
                {
                    minutesToAdd = parsedMinutes;
                }

                // Add the minutes to the archive time
                calEvent.ArchiveTime += minutesToAdd;

                // Update the event in the database
                await _eventManager.UpdateEventPropertyAsync(calEvent.Id, "ArchiveTime", calEvent.ArchiveTime);

                // Send confirmation message
                await message.Channel.SendMessageAsync($"Added {minutesToAdd} minutes to the archive time.");
                Console.WriteLine($"Added {minutesToAdd} minutes to archive time for event '{calEvent.Title}'");
                break;

            
            default:
                
                break;
        }
    }

    // End the Discord event
    private async Task EndEvent(CalendarEvent calEvent)
    {
        SocketGuildEvent guildEvent = GetGuildEventById(calEvent.ServerID, calEvent.DiscordEventId);
        await guildEvent.EndAsync();
    }

    // Close the Discord Thread
    private async Task CloseThread(CalendarEvent calEvent)
    {
        SocketThreadChannel threadChannel = Client.GetChannel(calEvent.DiscordThreadId) as SocketThreadChannel;
        await threadChannel.ModifyAsync(props =>
        {
            props.Archived = true;
            props.Locked = true;
        });
    }
    // Handle scheduled events
    private async Task GuildScheduledEventStartedAsync(SocketGuildEvent guildEvent)
    {
        Console.WriteLine($"Discord event started early: {guildEvent.Name} (ID: {guildEvent.Id})");

        // Find the corresponding calendar event
        CalendarEvent calendarEvent = GetEventByDiscordEventId(guildEvent.Id);

        if (calendarEvent == null)
        {
            Console.WriteLine($"Could not find calendar event for Discord event ID: {guildEvent.Id}");
            return;
        }

        // Create thread for the event if not already created
        if (!calendarEvent.ThreadCreated)
        {
            await CreateThread(calendarEvent);
            Console.WriteLine($"Thread created for early-started event: {calendarEvent.Title}");
        }

        // Process the event lane (mark as completed after thread is created)
        await ProcessEventLaneForEarlyStart(calendarEvent);

        await Task.CompletedTask;
    }

    // Handle scheduled events
    private async Task GuildScheduledEventCompletedAsync(SocketGuildEvent guildEvent)
    {
        Console.WriteLine($"Discord event completed: {guildEvent.Name} (ID: {guildEvent.Id})");

        // Find the corresponding calendar event
        CalendarEvent calendarEvent = GetEventByDiscordEventId(guildEvent.Id);

        if (calendarEvent == null)
        {
            Console.WriteLine($"Could not find calendar event for Discord event ID: {guildEvent.Id}");
            return;
        }

        // Process the event lane (mark as completed)
        await ProcessEventLaneForCompletion(calendarEvent);

        await Task.CompletedTask;
    }

    // Handle scheduled events
    private async Task GuildScheduledEventCancelledAsync(SocketGuildEvent guildEvent)
    {
        Console.WriteLine($"Discord event cancelled: {guildEvent.Name} (ID: {guildEvent.Id})");

        // Find the corresponding calendar event
        CalendarEvent calendarEvent = GetEventByDiscordEventId(guildEvent.Id);

        if (calendarEvent == null)
        {
            Console.WriteLine($"Could not find calendar event for Discord event ID: {guildEvent.Id}");
            return;
        }

        // Process the event lane (mark as cancelled)
        await ProcessEventLaneForCancellation(calendarEvent);

        await Task.CompletedTask;
    }

    // Process an event lane when an event is started early
    private async Task ProcessEventLaneForEarlyStart(CalendarEvent calEvent)
    {
        try
        {
            Console.WriteLine($"Processing event lane for early-started event: {calEvent.Title}");

            // Create a dictionary of property updates if needed
            Dictionary<string, object> updates = new Dictionary<string, object>();

            // Mark this event as needing no further processing in the lane
            // This effectively "kills" the lane since the event is already handled

            // Save the updated properties
            if (updates.Count > 0)
            {
                await _eventManager.UpdateEventPropertiesAsync(calEvent.Id, updates);
            }

            Console.WriteLine($"Event lane processed for early-started event: {calEvent.Title}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing event lane for early start: {ex.Message}");
        }
    }

    // Process an event lane when an event is completed
    private async Task ProcessEventLaneForCompletion(CalendarEvent calEvent)
    {
        try
        {
            Console.WriteLine($"Processing event lane for completed event: {calEvent.Title}");

            // Create a dictionary of property updates if needed
            Dictionary<string, object> updates = new Dictionary<string, object>();

            // Set any properties you want to update for a completed event

            // Save the updated properties
            if (updates.Count > 0)
            {
                await _eventManager.UpdateEventPropertiesAsync(calEvent.Id, updates);
            }

            Console.WriteLine($"Event lane processed for completed event: {calEvent.Title}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing event lane for completion: {ex.Message}");
        }
    }

    // Process an event lane when an event is cancelled
    private async Task ProcessEventLaneForCancellation(CalendarEvent calEvent)
    {
        try
        {
            Console.WriteLine($"Processing event lane for cancelled event: {calEvent.Title}");

            // Create a dictionary of property updates if needed
            Dictionary<string, object> updates = new Dictionary<string, object>();

            // Set any properties you want to update for a cancelled event

            // Save the updated properties
            if (updates.Count > 0)
            {
                await _eventManager.UpdateEventPropertiesAsync(calEvent.Id, updates);
            }

            Console.WriteLine($"Event lane processed for cancelled event: {calEvent.Title}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing event lane for cancellation: {ex.Message}");
        }
    }

    // Get all events
    public static CalendarEvent GetEventByDiscordEventId(ulong discordEventId)
    {
        return _eventManager.GetEventByDiscordId(discordEventId);
    }

    // Get all interested users for a specific event
    private static async Task<List<ulong>> GetInterestedUsers(ulong discordEventId)
    {
        SocketGuildEvent guildEvent = GetGuildEventById(_guild.Id, discordEventId);
        List<ulong> interestedUsers = new List<ulong>();

        if (guildEvent == null)
        {
            Console.WriteLine($"Could not find guild event with ID {discordEventId}");
            return interestedUsers;
        }

        var users = await guildEvent.GetUsersAsync(100, null);

        // Add each user's ID to the list
        foreach (var user in users)
        {
            interestedUsers.Add(user.Id);
        }

        Console.WriteLine($"Found {interestedUsers.Count} interested users for event {discordEventId}");
        return interestedUsers;
    }

    // Set the guild and channel for the bot
    public static void SetGuild(ulong guildId)
    {
        // Convert the guild ID to a SocketGuild using the client
        _guild = _client.GetGuild(guildId);

        if (_guild == null)
        {
            Console.WriteLine($"Could not find guild with ID {guildId}.");
        }
        else
        {
            Console.WriteLine($"Guild set to: {_guild.Name}");
        }
    }

    // Get the guild
    public SocketGuild GetGuild()
    {
        if (_guild == null)
        {
            Console.WriteLine("Guild is not set.");
            return null;
        }
        return _guild;
    }

    // Set the channel for the bot
    public static void SetChannel(ulong channelId)
    {
        if (_guild == null)
        {
            Console.WriteLine("Guild is not set. Cannot find channel.");
            return;
        }

        _channel = _guild.GetTextChannel(channelId);

        if (_channel == null)
        {
            Console.WriteLine($"Could not find text channel with ID {channelId} in guild {_guild.Name}.");
        }
        else
        {
            Console.WriteLine($"Channel set to: {_channel.Name} in guild {_guild.Name}");
        }
    }

    // Get the channel
    public SocketTextChannel GetChannel()
    {
        if (_channel == null)
        {
            Console.WriteLine("Channel is not set.");
            return null;
        }
        return _channel;
    }

    // Get a guild event by its ID
    public static SocketGuildEvent GetGuildEventById(ulong guildId, ulong eventId)
    {
        // Get the guild first
        var guild = _client.GetGuild(guildId);

        if (guild == null)
        {
            Console.WriteLine($"Could not find guild with ID {guildId}.");
            return null;
        }

        // Get the event from the guild
        var guildEvent = guild.GetEvent(eventId);

        if (guildEvent == null)
        {
            Console.WriteLine($"Could not find event with ID {eventId} in guild {guild.Name}.");
            return null;
        }

        return guildEvent;
    }

    // Create a new event in Discord
    public static async Task CreateEvent(CalendarEvent calEvent)
    {
        // Check if the event already exists 2 different ways
        bool eventExists = EventExists(calEvent.DiscordEventId, calEvent.ServerID);
        if (calEvent.DiscordEventCreated || eventExists)
        {
            Console.WriteLine("Event already created.");
            return;
        }

        try
        {
            SetGuild(calEvent.ServerID);
            SetChannel(calEvent.ChannelID);
            // Create the event
            var guildEvent = await _guild.CreateEventAsync(
                name: calEvent.Title,
                startTime: calEvent.StartTime,
                endTime: calEvent.StartTime.AddDays(2),
                type: GuildScheduledEventType.External,
                description: calEvent.Description,
                location: calEvent.Location
            );

            calEvent.DiscordEventCreated = true;
            calEvent.DiscordEventId = guildEvent.Id;

            if (guildEvent == null)
            {
                Console.WriteLine($"Failed to create event '{calEvent.Title}'.");
                return;
            }

            // Generate the event link using the created event's ID
            string eventLink = $"https://discord.com/events/{_guild.Id}/{guildEvent.Id}";

            // Send the event link to the specified channel
            if (_channel != null)
            {
                await SendMessageAsync(eventLink + "\n ```Event created, click 'interested' if you want to be notified when the thread is created```", _channel);
            }

            Console.WriteLine($"Event '{calEvent.Title}' created successfully in guild '{_guild.Name}'.");

            // Update the event in the database
            await _jsonSaver.UpdateEventPropertyAsync(
                calEvent.DiscordEventId,
                calEvent.ServerID,
                calEvent.ChannelID,
                "DiscordEventCreated",
                calEvent.DiscordEventCreated
            );

            await _jsonSaver.UpdateEventPropertyAsync(
                calEvent.DiscordEventId,
                calEvent.ServerID,
                calEvent.ChannelID,
                "DiscordEventId",
                calEvent.DiscordEventId
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while creating the event '{calEvent.Title}': {ex.Message}");
        }
    }

    // Create a new thread in Disocord
    public static async Task CreateThread(CalendarEvent calEvent)
    {
        // Create a thread for the event
        if (calEvent.ThreadCreated || ThreadExists(calEvent.DiscordThreadId, calEvent.ServerID))
        {
            Console.WriteLine("Thread already created.");
            return;
        }

        try
        {
            SetGuild(calEvent.ServerID);
            SetChannel(calEvent.ChannelID);

            // Create a new thread in the channel
            var threadName = $"{calEvent.Title} - {calEvent.StartTime.ToString("yyyy-MM-dd HH:mm")}";
            var threadChannel = await _channel.CreateThreadAsync(
                threadName,
                ThreadType.PublicThread,
                ThreadArchiveDuration.OneDay
            );

            // Save the thread ID
            calEvent.ThreadCreated = true;
            calEvent.DiscordThreadId = threadChannel.Id;

            // Update the event in the database
            await _jsonSaver.UpdateEventPropertyAsync(
                calEvent.DiscordEventId,
                calEvent.ServerID,
                calEvent.ChannelID,
                "ThreadCreated",
                calEvent.ThreadCreated
            );

            await _jsonSaver.UpdateEventPropertyAsync(
                calEvent.DiscordEventId,
                calEvent.ServerID,
                calEvent.ChannelID,
                "DiscordThreadId",
                calEvent.DiscordThreadId
            );

            // Get interested users and notify them
            List<ulong> interestedUsers = await GetInterestedUsers(calEvent.DiscordEventId);
            calEvent.IntresetedUsers = interestedUsers;

            // Save interested users to the event
            await _jsonSaver.UpdateEventPropertyAsync(
                calEvent.DiscordEventId,
                calEvent.ServerID,
                calEvent.ChannelID,
                "IntresetedUsers",
                calEvent.IntresetedUsers
            );

            // Notify interested users
            string message = "";
            foreach (var user in interestedUsers)
            {
                message += $"<@{user}> ";
            }

            if (!string.IsNullOrEmpty(message))
            {
                message += "\nThe event is starting soon!";
                await threadChannel.SendMessageAsync(message);
            }

            Console.WriteLine($"Thread created for event '{calEvent.Title}' and notified {interestedUsers.Count} users.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while creating the thread for event '{calEvent.Title}': {ex.Message}");
        }
        calEvent.ArchiveTime = 1440; // Set the archive time to 1 day
    }
    
    // Send a message to a specific channel
    public static Task SendMessageAsync(string message, SocketTextChannel channel)
    {
        if (channel == null)
        {
            Console.WriteLine("Cannot send message: Channel is null");
            return Task.CompletedTask;
        }

        // send a message
        channel.SendMessageAsync(message);
        return Task.CompletedTask;
    }

    // Check if an event or thread already exists
    private static bool EventExists(ulong eventId, ulong guildId)
    {
        // Check if an event already exists by checking if we can find it in the guild
        try
        {
            var guild = _client.GetGuild(guildId);
            if (guild == null)
            {
                Console.WriteLine($"Could not find guild with ID {guildId}.");
                return false;
            }

            var guildEvent = guild.GetEvent(eventId);

            // If the event is found, it exists
            return guildEvent != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking if event exists: {ex.Message}");
            return false;
        }
    }

    // Check if a thread already exists
    private static bool ThreadExists(ulong threadId, ulong guildId)
    {
        // Check if a thread already exists by checking if we can find it in the guild
        try
        {
            var guild = _client.GetGuild(guildId);
            if (guild == null)
            {
                Console.WriteLine($"Could not find guild with ID {guildId}.");
                return false;
            }

            // Check if the thread exists in the guild's threads
            var thread = guild.ThreadChannels.FirstOrDefault(t => t.Id == threadId);

            // If the thread is found, it exists
            return thread != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking if thread exists: {ex.Message}");
            return false;
        }
    }
}