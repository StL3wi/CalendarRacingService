# Calendar Racing Service

A disocrd bot that syncs with a google calendar to create discord events and threads

## Feature List
- Automatically creates Discord events from Google Calendar
- Creates Discord threads for upcoming events
- Notifies interested users


## Setup
1. Clone the repository
2. Rename the 'config.Template.json' to 'config.json', and fill in the values with your own [values](https://github.com/StL3wi/CalendarRacingService?tab=readme-ov-file#the-configjson-file)
3. [get your credentials.json from the google cloud console](https://developers.google.com/workspace/guides/create-credentials)
4. Run the bot.

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
- DebugMode - obsolute

