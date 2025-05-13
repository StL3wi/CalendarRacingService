using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public class GoogleCalendarService
{
    private readonly CalendarService _service;
    private static Config _config;

    public GoogleCalendarService(string credentialsPath)
    {
        UserCredential credential;
        _config = JsonLoader.LoadJson<Config>("config");
        using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
        {
            Console.WriteLine("Reading credentials from: " + credentialsPath);

            string credPath = "token.json";
            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.Load(stream).Secrets,
                new[] { CalendarService.Scope.CalendarReadonly },
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true)).Result;

            Console.WriteLine("Authorization completed.");
        }

        _service = new CalendarService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Calendar Racing Service"
        });

        Console.WriteLine("CalendarService initialized.");
    }

    public async Task<List<CalendarEvent>> GetUpcomingEventsAsync(string calendarId)
    {
        JsonSaver jsonSaver = new JsonSaver();
        EventsResource.ListRequest request = _service.Events.List(calendarId);
        request.TimeMin = DateTime.Now;
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        Events events = await request.ExecuteAsync();
        List<CalendarEvent> calendarEvents = new List<CalendarEvent>();
        if (events.Items != null && events.Items.Count > 0)
        {
            foreach (var eventItem in events.Items)
            {
                DateTime start = eventItem.Start.DateTime ?? DateTime.Parse(eventItem.Start.Date);
                var calendarEvent = new CalendarEvent
                {
                    Id = eventItem.Id,
                    Title = eventItem.Summary,
                    Description = eventItem.Description,
                    StartTime = start,
                    Location = eventItem.Location,
                    ChannelID = _config.ChannelId,
                    ServerID = _config.ServerId
                };
                calendarEvents.Add(calendarEvent);

                // Check to see if the file is created && if it is the same
                
                bool fileExists = FileExists(calendarEvent.Id, calendarEvent.ServerID, calendarEvent.ChannelID);
                if (!fileExists)
                {
                    await jsonSaver.SaveEventToJsonAsync(calendarEvent);
                }
                else
                {
                    Console.WriteLine($"Event {calendarEvent.Id} already exists in the file system.");
                }
                
            }
        }
        else { Console.WriteLine("\n No events found \n"); }
        return calendarEvents;
    }

    private static bool FileExists(string fileID, ulong serverId, ulong channelId)
    {
        string baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Events");
        string eventDirectory = Path.Combine(baseDirectory, serverId.ToString(), channelId.ToString());
        string safeFileName = fileID;
        string filePath = Path.Combine(eventDirectory, $"{safeFileName}.json");
        return File.Exists(filePath);
    }

    
}
