# GIFBot
GIFBot is an interactive Twitch bot written by https://twitch.tv/fiercekittenz and has been in development since early-2016. It is an ASP.NET application with a Blazor WASM front-end. As a caveat, this is a personal project developed in my fleeting spare time. This means you may see a lot of "TODO" commentary for things I absolutely intend to do, but have yet to find the time or a stable method of cloning myself. 

## High Level Architecture

### Server
The GIFBot.Server project is the backbone. It's using SignalR to allow for bi-directional communication with the Client. The server is made up of "Features" that have their own data, threads, and core functionality. These features are added to the GIFBot instance, which is the core class managing the Twitch connection and feature access. The GIFBotHub is your SignalR hub for receiving messaging from the client.

### Client
The Blazor client provides several browser source pages. In order to avoid "chicken vs egg" problems on startup order, the browser source that users have for broadcaster software is an HTML page that periodically pings the server to see if it is running or not. Once it successfully gets a response, it navigates to the Blazor page that will handle the various feature. Only a handful of features have front-end components to them (animations, stickers, countdown, goalbar, etc.). This is done so that if you open OBS before GIFBot, your browser sources do not report errors, requiring a refresh from cache.

### Utility and Shared Code
Anything that needs to be used by both the client and server projects is in the Shared project. This includes data models and utility classes.

## Planning and Future Releases
Typically I alternate my release planning to allow for one iteration of feature additions and community requests, with a second iteration where I perform architectural improvements. I publicly track tasks for each release as well as the backlog on Trello: https://trello.com/b/KMmvrIgA/gifbot-r Do not hesitate to start a conversation or issue here on Github if there's something you'd like to suggest!

## Contributing
If you wish to help with GIFBot, you will need a license for the Progress Telerik Blazor UI Components. They are very powerful components provided courtesy of Progress Telerik and make up the bulk of the client-side. You can obtain a 30-day license to trial it, but you'll have to qualify the namespace to the trial version - make sure not to include that namespace change in your pull requests.
