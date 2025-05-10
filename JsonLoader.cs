using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;

public static class JsonLoader
{
    public static T LoadJson<T>(string jsonName)
    {
        try
        {
            string jsonLocation = AppDomain.CurrentDomain.BaseDirectory;
            string jsonPath = Path.Combine(jsonLocation, jsonName + ".json");
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException($"File not found at {jsonPath}");
            string jsonContent = File.ReadAllText(jsonPath);
            T result = JsonConvert.DeserializeObject<T>(jsonContent);
            if (result == null)
                throw new Exception($"File could not be deserialized to type {typeof(T).Name}. Check the JSON structure.");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading JSON {jsonName}: {ex.Message}");
            return default;
        }
    }

    public static T LoadEventJson<T>(string eventId, ulong serverId, ulong channelId)
    {
        try
        {
            // Create the path based on the directory structure: Events/{ServerID}/{ChannelID}/{EventID}.json
            string baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Events");
            string eventDirectory = Path.Combine(baseDirectory, serverId.ToString(), channelId.ToString());
            string safeFileName = MakeSafeFileName(eventId);
            string jsonPath = Path.Combine(eventDirectory, $"{safeFileName}.json");

            if (!File.Exists(jsonPath))
                throw new FileNotFoundException($"Event file not found at {jsonPath}");

            string jsonContent = File.ReadAllText(jsonPath);
            T result = JsonConvert.DeserializeObject<T>(jsonContent);

            if (result == null)
                throw new Exception($"Event file could not be deserialized to type {typeof(T).Name}. Check the JSON structure.");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading event: {ex.Message}");
            return default;
        }
    }

    public static List<T> LoadAllEventsForChannel<T>(ulong serverId, ulong channelId)
    {
        try
        {
            List<T> events = new List<T>();

            // Create the path to the channel directory
            string baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Events");
            string channelDirectory = Path.Combine(baseDirectory, serverId.ToString(), channelId.ToString());

            if (!Directory.Exists(channelDirectory))
            {
                Console.WriteLine($"Channel directory not found: {channelDirectory}");
                return events;
            }

            foreach (string file in Directory.GetFiles(channelDirectory, "*.json"))
            {
                try
                {
                    string jsonContent = File.ReadAllText(file);
                    T eventItem = JsonConvert.DeserializeObject<T>(jsonContent);

                    if (eventItem != null)
                    {
                        events.Add(eventItem);
                        Console.WriteLine($"Loaded event from {file}");
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
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading events for channel: {ex.Message}");
            return new List<T>();
        }
    }

    // Helper method to create a valid filename from the event ID
    private static string MakeSafeFileName(string fileName)
    {
        // Replace invalid filename characters
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }
}