using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CalendarRacingService
{
    public class Program
    {
        private static Config _config;
        private static GoogleCalendarService _googleCalendarService;
        private static DiscordService _discordService;
        private static JsonSaver _jsonSaver;
        private static EventManager _eventManager;
        private static bool _debugMode = false;
        private static Timer _calendarRefreshTimer;
        private static Timer _eventLaneTimer;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Calendar Racing Service...");

            try
            {
                // Step 1: Load configuration
                LoadJsons();
                _jsonSaver = new JsonSaver();

                // Step 2: Initialize EventManager
                _eventManager = new EventManager(_config, _jsonSaver);
                DiscordService._eventManager = _eventManager;
                DiscordService._jsonSaver = _jsonSaver;

                // Step 3: Initialize Google Calendar Service
                await InitializeGoogleCalendarService();

                // Step 4: Initialize Discord Service
                await InitializeDiscordService();

                // Step 5: Start the refresh timers
                StartTimers();

                Console.WriteLine("Service started successfully. Press any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error starting service: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            finally
            {
                // Clean up timers when exiting
                _calendarRefreshTimer?.Dispose();
                _eventLaneTimer?.Dispose();
            }
        }

        private static void LoadJsons()
        {
            try
            {
                _config = JsonLoader.LoadJson<Config>("config");
                if (_config == null)
                {
                    throw new Exception("Failed to load config. Ensure config.json exists and is properly formatted.");
                }

                _debugMode = _config.DebugMode;
                Console.WriteLine("Config loaded successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading JSON files: {ex.Message}");
                throw; // Re-throw to stop the application if config can't be loaded
            }
        }

        private static async Task InitializeGoogleCalendarService()
        {
            try
            {
                _googleCalendarService = new GoogleCalendarService("credentials.json");

                // Get initial events from Google Calendar
                var calendarEvents = await _googleCalendarService.GetUpcomingEventsAsync(_config.CalendarId);
                Console.WriteLine($"Loaded {calendarEvents.Count} upcoming events from Google Calendar");

                // Sync the retrieved events with our EventManager
                await _eventManager.SyncWithCalendarEventsAsync(calendarEvents);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Google Calendar Service: {ex.Message}");
                throw;
            }
        }

        private static async Task InitializeDiscordService()
        {
            try
            {
                _discordService = new DiscordService(_config, _googleCalendarService);
                await _discordService.StartClientAsync();

                // Wait for Discord to be ready (this could be improved with a proper async signal) // *** FUTURE ME FIX THIS ***
                await Task.Delay(2000);

                // Initial synchronization of events
                await ProcessEventLanes();
                Console.WriteLine("Discord Service initialized and event lanes organized.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Discord Service: {ex.Message}");
                throw;
            }
        }

        private static void StartTimers()
        {
            // Timer for refreshing calendar events
            double calendarRefreshMs = _config.CalendarRefresh * 60 * 1000; // Convert minutes to milliseconds
            _calendarRefreshTimer = new Timer(async _ => await RefreshCalendarEvents(), null,
                TimeSpan.FromMilliseconds(calendarRefreshMs), TimeSpan.FromMilliseconds(calendarRefreshMs));

            // Timer for processing event lanes
            double eventLaneMs = _config.RefreshTimer * 60 * 1000; // Convert minutes to milliseconds
            _eventLaneTimer = new Timer(async _ => await ProcessEventLanes(), null,
                TimeSpan.FromMilliseconds(eventLaneMs), TimeSpan.FromMilliseconds(eventLaneMs));

            Console.WriteLine($"Timers started: Calendar refresh every {_config.CalendarRefresh} minutes, Event lane processing every {_config.RefreshTimer} minutes.");
        }

        private static async Task RefreshCalendarEvents()
        {
            try
            {
                Console.WriteLine("Refreshing calendar events...");
                var updatedEvents = await _googleCalendarService.GetUpcomingEventsAsync(_config.CalendarId);

                // Sync with EventManager
                await _eventManager.SyncWithCalendarEventsAsync(updatedEvents);

                // Clean up old events
                await _eventManager.CleanupOldEventsAsync(_config.MaxDays);

                Console.WriteLine($"Calendar refresh complete. {_eventManager.GetAllEvents().Count} active events.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing calendar events: {ex.Message}");
            }
        }

        private static async Task ProcessEventLanes()
        {
            try
            {
                Console.WriteLine("Processing event lanes...");

                // Get events that are coming up within the MaxDays window
                var upcomingEvents = _eventManager.GetAllEvents();
                var eventLanes = new Dictionary<string, CalendarEvent>();

                foreach (var calEvent in upcomingEvents)
                {
                    TimeSpan timeToEvent = calEvent.StartTime - DateTime.Now;

                    // Only process events within MaxDays window
                    if (timeToEvent.TotalDays <= _config.MaxDays && timeToEvent.TotalHours > 0)
                    {
                        eventLanes.Add(calEvent.Id, calEvent);

                        // Create Discord event if it's within EventCreateTime window
                        if (!calEvent.DiscordEventCreated && timeToEvent.TotalHours <= _config.EventCreateTime)
                        {
                            await DiscordService.CreateEvent(calEvent);
                            Console.WriteLine($"Created Discord event for {calEvent.Title} (starts in {timeToEvent.TotalHours:F1} hours)");
                        }

                        // Create Discord thread if it's within ThreadCreateTime window
                        if (calEvent.DiscordEventCreated && !calEvent.ThreadCreated &&
                            timeToEvent.TotalHours <= _config.ThreadCreateTime)
                        {
                            await DiscordService.CreateThread(calEvent);
                            Console.WriteLine($"Created Discord thread for {calEvent.Title} (starts in {timeToEvent.TotalHours:F1} hours)");
                        }
                    }
                }

                Console.WriteLine($"Event lanes processed. {eventLanes.Count} events in active lanes.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing event lanes: {ex.Message}");
            }
        }

        // Utility method to log event lanes status
        private static void LogEventLanesStatus(Dictionary<string, CalendarEvent> eventLanes)
        {
            if (_debugMode)
            {
                Console.WriteLine($"== Current Event Lanes Status ==");
                foreach (var lane in eventLanes)
                {
                    var calEvent = lane.Value;
                    TimeSpan timeToEvent = calEvent.StartTime - DateTime.Now;

                    Console.WriteLine($"Lane: {calEvent.Id}");
                    Console.WriteLine($"  Event: {calEvent.Title}");
                    Console.WriteLine($"  Start: {calEvent.StartTime} (in {timeToEvent.TotalHours:F1} hours)");
                    Console.WriteLine($"  Discord Event: {(calEvent.DiscordEventCreated ? "Created" : "Not Created")}");
                    Console.WriteLine($"  Discord Thread: {(calEvent.ThreadCreated ? "Created" : "Not Created")}");
                    Console.WriteLine();
                }
            }
        }
    }
}