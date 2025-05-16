using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class EventManager
{
    private readonly Dictionary<string, CalendarEvent> _events;
    private readonly JsonSaver _jsonSaver;
    private readonly Config _config;
    private bool _debugMode;

    public EventManager(Config config, JsonSaver jsonSaver = null)
    {
        _events = new Dictionary<string, CalendarEvent>();
        _config = config;
        _debugMode = config.DebugMode;
        _jsonSaver = jsonSaver ?? new JsonSaver();

        // Load events from disk on initialization
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            // Load all saved events from disk
            List<CalendarEvent> savedEvents = _jsonSaver.LoadAllEvents();

            foreach (var evt in savedEvents)
            {
                _events[evt.Id] = evt;
            }

            if (_debugMode)
            {
                Console.WriteLine($"EventManager initialized with {_events.Count} events from disk");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing EventManager: {ex.Message}");
        }
    }

    // Add or update an event in the dictionary
    public async Task AddOrUpdateEventAsync(CalendarEvent calendarEvent)
    {
        if (calendarEvent == null) throw new ArgumentNullException(nameof(calendarEvent));

        bool isNewEvent = !_events.ContainsKey(calendarEvent.Id);

        if (!isNewEvent)
        {
            // Preserve Discord-related state if updating an existing event
            var existingEvent = _events[calendarEvent.Id];
            calendarEvent.DiscordEventCreated = existingEvent.DiscordEventCreated;
            calendarEvent.DiscordEventId = existingEvent.DiscordEventId;
            calendarEvent.ThreadCreated = existingEvent.ThreadCreated;
            calendarEvent.DiscordThreadId = existingEvent.DiscordThreadId;
            calendarEvent.IntresetedUsers = existingEvent.IntresetedUsers;
        }

        // Update the timestamp
        calendarEvent.UpdateTimestamp();

        // Add/update in memory
        _events[calendarEvent.Id] = calendarEvent;

        // Save to disk
        await _jsonSaver.SaveEventToJsonAsync(calendarEvent);

        if (_debugMode)
        {
            Console.WriteLine($"{(isNewEvent ? "Added" : "Updated")} event: {calendarEvent.Title}");
        }
    }

    // Get an event by ID
    public CalendarEvent GetEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) throw new ArgumentNullException(nameof(eventId));

        _events.TryGetValue(eventId, out var calendarEvent);
        return calendarEvent;
    }

    // Get an event by Discord event ID
    public CalendarEvent GetEventByDiscordId(ulong discordEventId)
    {
        return _events.Values.FirstOrDefault(e => e.DiscordEventId == discordEventId);
    }

    // Get an event ID by Discord thread ID
    public string GetEventIdByDiscordThreadId(ulong discordThreadId)
    {
        return _events.Values.FirstOrDefault(e => e.DiscordThreadId == discordThreadId)?.Id;
    }

    // Get an event by Discord thread ID
    public CalendarEvent GetEventByDiscordThreadId(ulong discordThreadId)
    {
        return _events.Values.FirstOrDefault(e => e.DiscordThreadId == discordThreadId);
    }

    // Get all events
    public List<CalendarEvent> GetAllEvents()
    {
        return _events.Values.ToList();
    }

    // Get upcoming events within a specific time window
    public List<CalendarEvent> GetUpcomingEvents(int hours = 24)
    {
        DateTime cutoff = DateTime.Now.AddHours(hours);
        return _events.Values
            .Where(e => e.StartTime > DateTime.Now && e.StartTime <= cutoff)
            .OrderBy(e => e.StartTime)
            .ToList();
    }

    // Update a specific property of an event
    public async Task UpdateEventPropertyAsync(string eventId, string propertyName, object propertyValue)
    {
        if (string.IsNullOrEmpty(eventId)) throw new ArgumentNullException(nameof(eventId));
        if (string.IsNullOrEmpty(propertyName)) throw new ArgumentNullException(nameof(propertyName));

        if (!_events.TryGetValue(eventId, out var calendarEvent))
        {
            throw new KeyNotFoundException($"Event with ID {eventId} not found");
        }

        // Use reflection to update the property
        var property = typeof(CalendarEvent).GetProperty(propertyName);
        if (property == null)
        {
            throw new ArgumentException($"Property {propertyName} not found on CalendarEvent");
        }

        // Update the property
        property.SetValue(calendarEvent, propertyValue);

        // Update timestamp
        calendarEvent.UpdateTimestamp();

        // Persist to disk
        await _jsonSaver.UpdateEventPropertyAsync(
            Convert.ToUInt64(eventId),
            calendarEvent.ServerID,
            calendarEvent.ChannelID,
            propertyName,
            propertyValue
        );

        if (_debugMode)
        {
            Console.WriteLine($"Updated property {propertyName} for event: {calendarEvent.Title}");
        }
    }

    // Update multiple properties at once
    public async Task UpdateEventPropertiesAsync(string eventId, Dictionary<string, object> propertyUpdates)
    {
        if (string.IsNullOrEmpty(eventId)) throw new ArgumentNullException(nameof(eventId));
        if (propertyUpdates == null || propertyUpdates.Count == 0) throw new ArgumentException("No properties to update");

        if (!_events.TryGetValue(eventId, out var calendarEvent))
        {
            throw new KeyNotFoundException($"Event with ID {eventId} not found");
        }

        // Update each property in memory
        foreach (var update in propertyUpdates)
        {
            var property = typeof(CalendarEvent).GetProperty(update.Key);
            if (property == null)
            {
                Console.WriteLine($"Property {update.Key} not found on CalendarEvent - skipping");
                continue;
            }

            property.SetValue(calendarEvent, update.Value);
        }

        // Update timestamp
        calendarEvent.UpdateTimestamp();

        // Persist to disk
        await _jsonSaver.UpdateEventPropertiesAsync(
            eventId,
            calendarEvent.ServerID,
            calendarEvent.ChannelID,
            propertyUpdates
        );

        if (_debugMode)
        {
            Console.WriteLine($"Updated {propertyUpdates.Count} properties for event: {calendarEvent.Title}");
        }
    }

    // Sync with refreshed Google Calendar events
    public async Task SyncWithCalendarEventsAsync(List<CalendarEvent> calendarEvents)
    {
        if (calendarEvents == null) throw new ArgumentNullException(nameof(calendarEvents));

        int newCount = 0;
        int updatedCount = 0;

        foreach (var newEvent in calendarEvents)
        {
            if (_events.TryGetValue(newEvent.Id, out var existingEvent))
            {
                // Update existing event's calendar properties while preserving Discord state
                existingEvent.Title = newEvent.Title;
                existingEvent.Description = newEvent.Description;
                existingEvent.StartTime = newEvent.StartTime;
                existingEvent.Location = newEvent.Location;
                existingEvent.UpdateTimestamp();

                await _jsonSaver.SaveEventToJsonAsync(existingEvent);
                updatedCount++;
            }
            else
            {
                // New event
                await AddOrUpdateEventAsync(newEvent);
                newCount++;
            }
        }

        if (_debugMode)
        {
            Console.WriteLine($"Calendar sync complete: {newCount} new events, {updatedCount} updated events");
        }
    }

    // Remove old events that have passed
    public async Task CleanupOldEventsAsync(int daysToKeep = 7)
    {
        DateTime cutoff = DateTime.Now.AddDays(-daysToKeep);
        var oldEvents = _events.Values.Where(e => e.StartTime < cutoff).ToList();

        foreach (var oldEvent in oldEvents)
        {
            _events.Remove(oldEvent.Id);
            await _jsonSaver.DeleteEventAsync(oldEvent.Id, oldEvent.ServerID, oldEvent.ChannelID);
        }

        if (_debugMode && oldEvents.Any())
        {
            Console.WriteLine($"Cleaned up {oldEvents.Count} old events");
        }
    }
}