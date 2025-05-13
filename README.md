# Calendar Racing Service

A discord bot that syncs with a google calendar to create discord events and threads
This is a work in progress. The bot does work, but there are some bugs and features that I want to add. 
I am open to suggestions and pull requests.
I have been working on this bot for over a year with different improvements and features with each version.
There used to be a version with SQL, but I have since moved to using JSON files for the events as I found it might be easier for each user to work with.


----------------------------------------------------------------------------------------
	

## Feature List
- Automatically creates Discord events from Google Calendar
- Creates Discord threads for upcoming events
- Notifies interested users


## Setup
1. Clone the repository
2. Rename the 'configTemplate.json' to 'config.json', and fill in the values with your own [values](https://github.com/StL3wi/CalendarRacingService?tab=readme-ov-file#the-configjson-file)
3. [get your credentials.json from the google cloud console](https://developers.google.com/workspace/guides/create-credentials)
4. Run the bot.



## General Use Features
- **Debug Mode** - Adds more debugging to the console. Saves all logs to a file, and allows the user to test features on a different server
- **ResetOnRestart** - The bot will delete all JSON files for the calendar events and start fresh
- **DebugLogging** - The bot will log extra events to the console and to a file


## The config.json file
- DiscordToken - [The token for the discord bot](https://docs.discordbotstudio.org/setting-up-dbs/finding-your-bot-token)
- GoogleKey - [the key for the google calendar api](https://support.google.com/googleapi/answer/6158862?hl=en)
- CalendarId - [The id of the google calendar to sync with](https://docs.simplecalendar.io/find-google-calendar-id/)
- ChannelId - [The id of the discord channel to send threads and events to](https://support.discord.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID)
- ServerId - [The id of the discord server to send the threads and events to](https://support.discord.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID)
- ThreadCreateTime - The time in hours to create the thread before the Google calendar event starts
- EventCreateTime - The time in hours to create the Discord event before the Google calendar event starts
- RefreshTimer - The time in minutes to refresh the bot to see if a event is ready to be created
- CalendarRefresh - The time in minutes to refresh the calendar to see if a anything has changed
- ConsoleLogging - If true, the bot will log extra to the console
- DebugLogging - If true, the bot will log extra debug stuff to the console
- ResetOnRestart - 
- TagedUsers - A list of users to tag in for each thread created
- AdminId - a list of discord user ids that are allowed to use the admin commands
- MaxDays - The max days to look ahead for events
- DebugMode - adds more debugging to the console. primarily used for myself when programming. future edition this will not allow the bot to create any discord events or threads while on
- BotAdminID - The id of the bot admin -- used for future debugging features


----------------------------------------------------------------------------------------
# Future Features

## Admin DM Commands
- `!EndAll` - Ends all events
- `!CloseAll` - Closes all open threads
- `!AddEvent` - Adds event to the Google Calendar
- `!RemoveEvent` - Removes event from the Google Calendar
- `!EditEvent` - Edits event in the Google Calendar

## Admin Commands
- `!end` - Ends the current event
- `!close` - Closes the current thread
  - *These are in the works as of 5/12*

## User Commands
- `!Info` - Returns information about the event
- `!IntrestedAll` - Adds the user to the config.json file under the TaggedUsers list to always be tagged in all future events
  - The bot will return a message confirming the user's decision, as this may be a lot of pings
- `!NotIntrestedAll` - Removes the user from the config.json file under the TaggedUsers list to always be tagged in all future events
- `!Upcoming` - Returns a list of all upcoming events (Range will be set in the config.json file)

## Future Improvements
- Improve and implement Serilog logging
- Improve and implement the console to be more user friendly
