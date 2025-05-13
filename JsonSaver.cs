using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class JsonSaver
{
    private readonly string _baseDirectory;

    public JsonSaver(string baseDirectory = null)
    {
        // If no directory is specified, use a default "Events" folder in the application directory
        _baseDirectory = baseDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Events");

        // Ensure the base directory exists
        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
            Console.WriteLine($"Created base directory: {_baseDirectory}");
        }
    }

    public async Task SaveCalendarEventsAsync(List<CalendarEvent> events)
    {
        if (events == null || events.Count == 0)
        {
            Console.WriteLine("No events to save.");
            return;
        }

        Console.WriteLine($"Saving {events.Count} events to JSON files...");

        foreach (var calendarEvent in events)
        {
            await SaveEventToJsonAsync(calendarEvent);
        }

        Console.WriteLine("All events saved successfully.");
    }

    public async Task SaveEventToJsonAsync(CalendarEvent calendarEvent)
    {
        if (calendarEvent == null)
        {
            throw new ArgumentNullException(nameof(calendarEvent));
        }

        // Get ServerID and ChannelID from the event
        // Use default values of 0 if they're not set
        ulong serverId = calendarEvent.ServerID != 0 ? calendarEvent.ServerID : 0;
        ulong channelId = calendarEvent.ChannelID != 0 ? calendarEvent.ChannelID : 0;

        // Create the directory structure: Events/{ServerID}/{ChannelID}
        string eventDirectory = Path.Combine(_baseDirectory, serverId.ToString(), channelId.ToString());

        // Ensure the directory exists
        if (!Directory.Exists(eventDirectory))
        {
            Directory.CreateDirectory(eventDirectory);
            Console.WriteLine($"Created directory: {eventDirectory}");
        }

        // Create a safe filename from the event ID
        string safeFileName = MakeSafeFileName(calendarEvent.Id);
        string filePath = Path.Combine(eventDirectory, $"{safeFileName}.json");

        try
        {
            // Serialize the event to JSON with pretty formatting
            string json = JsonConvert.SerializeObject(calendarEvent, Formatting.Indented);

            // Write to file asynchronously
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                await file.WriteAsync(json);
            }

            Console.WriteLine($"Saved event: {calendarEvent.Title} to {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving event {calendarEvent.Title}: {ex.Message}");
            throw;
        }
    }

    // Helper method to create a valid filename from the event ID
    private string MakeSafeFileName(string fileName)
    {
        // Replace invalid filename characters
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }

    // Load all events from all servers and channels
    public List<CalendarEvent> LoadAllEvents()
    {
        List<CalendarEvent> events = new List<CalendarEvent>();

        if (!Directory.Exists(_baseDirectory))
        {
            Console.WriteLine($"Base directory not found: {_baseDirectory}");
            return events;
        }

        // Loop through all server directories
        foreach (string serverDir in Directory.GetDirectories(_baseDirectory))
        {
            // Loop through all channel directories in this server
            foreach (string channelDir in Directory.GetDirectories(serverDir))
            {
                // Loop through all JSON files in this channel
                foreach (string file in Directory.GetFiles(channelDir, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        CalendarEvent calendarEvent = JsonConvert.DeserializeObject<CalendarEvent>(json);

                        if (calendarEvent != null)
                        {
                            events.Add(calendarEvent);
                            Console.WriteLine($"Loaded event: {calendarEvent.Title} from {file}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading file {file}: {ex.Message}");
                        // Continue loading other files even if one fails
                    }
                }
            }
        }

        return events;
    }

    // Load all events for a specific server and channel
    public List<CalendarEvent> LoadEventsForChannel(ulong serverId, ulong channelId)
    {
        List<CalendarEvent> events = new List<CalendarEvent>();

        string channelDirectory = Path.Combine(_baseDirectory, serverId.ToString(), channelId.ToString());

        if (!Directory.Exists(channelDirectory))
        {
            Console.WriteLine($"Channel directory not found: {channelDirectory}");
            return events;
        }

        foreach (string file in Directory.GetFiles(channelDirectory, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(file);
                CalendarEvent calendarEvent = JsonConvert.DeserializeObject<CalendarEvent>(json);

                if (calendarEvent != null)
                {
                    events.Add(calendarEvent);
                    Console.WriteLine($"Loaded event: {calendarEvent.Title} from {file}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading file {file}: {ex.Message}");
                // Continue loading other files even if one fails
            }
        }

        return events;
    }

    // Load a specific event by ID, server ID, and channel ID
    public CalendarEvent LoadEvent(string eventId, ulong serverId, ulong channelId)
    {
        string safeFileName = MakeSafeFileName(eventId);
        string filePath = Path.Combine(_baseDirectory, serverId.ToString(), channelId.ToString(), $"{safeFileName}.json");

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Event file not found: {filePath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<CalendarEvent>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading event {eventId}: {ex.Message}");
            return null;
        }
    }

    // Edit a specific property of an existing event JSON
    public async Task<bool> UpdateEventPropertyAsync(ulong eventId, ulong serverId, ulong channelId,
        string propertyName, object propertyValue)
    {
        string safeFileName = MakeSafeFileName(eventId.ToString());
        string filePath = Path.Combine(_baseDirectory, serverId.ToString(), channelId.ToString(), $"{safeFileName}.json");

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Event file not found: {filePath}");
            return false;
        }

        try
        {
            // Read the existing JSON file
            string json = File.ReadAllText(filePath);
            CalendarEvent calendarEvent = JsonConvert.DeserializeObject<CalendarEvent>(json);

            if (calendarEvent == null)
            {
                Console.WriteLine($"Failed to deserialize event: {filePath}");
                return false;
            }

            // Use reflection to update the property
            var property = typeof(CalendarEvent).GetProperty(propertyName);
            if (property == null)
            {
                Console.WriteLine($"Property {propertyName} not found on CalendarEvent");
                return false;
            }

            // Set the new value
            property.SetValue(calendarEvent, propertyValue);

            // Save the updated object back to the file
            string updatedJson = JsonConvert.SerializeObject(calendarEvent, Formatting.Indented);

            // Write to file asynchronously
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                await file.WriteAsync(updatedJson);
            }

            Console.WriteLine($"Updated {propertyName} for event: {calendarEvent.Title}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating event {eventId}: {ex.Message}");
            return false;
        }
    }

    // Update multiple properties at once with a dictionary
    public async Task<bool> UpdateEventPropertiesAsync(string eventId, ulong serverId, ulong channelId,
        Dictionary<string, object> propertyUpdates)
    {
        string safeFileName = MakeSafeFileName(eventId);
        string filePath = Path.Combine(_baseDirectory, serverId.ToString(), channelId.ToString(), $"{safeFileName}.json");

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Event file not found: {filePath}");
            return false;
        }

        try
        {
            // Read the existing JSON file
            string json = File.ReadAllText(filePath);
            CalendarEvent calendarEvent = JsonConvert.DeserializeObject<CalendarEvent>(json);

            if (calendarEvent == null)
            {
                Console.WriteLine($"Failed to deserialize event: {filePath}");
                return false;
            }

            bool anyUpdated = false;

            // Update each property in the dictionary
            foreach (var update in propertyUpdates)
            {
                var property = typeof(CalendarEvent).GetProperty(update.Key);
                if (property == null)
                {
                    Console.WriteLine($"Property {update.Key} not found on CalendarEvent");
                    continue;
                }

                // Set the new value
                property.SetValue(calendarEvent, update.Value);
                anyUpdated = true;
            }

            if (!anyUpdated)
            {
                Console.WriteLine("No valid properties were updated");
                return false;
            }

            // Save the updated object back to the file
            string updatedJson = JsonConvert.SerializeObject(calendarEvent, Formatting.Indented);

            // Write to file asynchronously
            using (StreamWriter file = new StreamWriter(filePath, false))
            {
                await file.WriteAsync(updatedJson);
            }

            Console.WriteLine($"Updated properties for event: {calendarEvent.Title}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating event {eventId}: {ex.Message}");
            return false;
        }
    }
}