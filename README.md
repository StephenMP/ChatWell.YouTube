# ChatWell.YouTube
A .NET Standard client library for obtaining live chat messages for YouTube Live Streams.

|Build|Release|
|:---:|:-----:|
|[![Build Status](https://travis-ci.org/StephenMP/ChatWell.YouTube.svg?branch=master)](https://travis-ci.org/StephenMP/ChatWell.YouTube)|[![Build Status](https://travis-ci.org/StephenMP/ChatWell.YouTube.svg?branch=release)](https://travis-ci.org/StephenMP/ChatWell.YouTube) [![NuGet](https://img.shields.io/nuget/v/ChatWell.YouTube.svg)](https://www.nuget.org/packages/ChatWell.YouTube/)|

## Prerequisite
This library includes the interface `IYouTubeAuthService` which has the method `Task<UserCredential> GetUserCredentialAsync()`. In order to create a new YouTubeChatClient, you must provide the constructor with your implementation of the `IYouTubeAuthService`.

This is done this way because there is more than one way to obtain YouTube User Credentials depending on the platform your code is running on (installed application or web application) and your authentication needs may vary. Below is a sample implementation of the `IYouTubeAuthService` that you can use as a starting point which uses the installed application OAuth flow.

```csharp
public class YoutubeAuthService : IYoutubeAuthService
{
    private readonly IDataStore dataStore;

    // Your IDataStore can be any class that implements Google's IDataStore
    public YoutubeAuthService(IDataStore dataStore)
    {
        this.dataStore = dataStore;
    }

    public async Task<UserCredential> GetUserCredentialAsync()
    {
        const string clientIdPath = @"C:\path\to\your\client_id.json";
        using (var stream = new FileStream(clientIdPath, FileMode.Open))
        {
            const string user = "whateveruser@gmail.com";
            var loadedSecrets = GoogleClientSecrets.Load(stream);
            var clientSecrets = loadedSecrets.Secrets;
            var scopes = new[] { YouTubeService.Scope.YoutubeForceSsl };
            var dataStore = this.dataStore;
            var user = await GoogleWebAuthorizationBroker.AuthorizeAsync(clientSecrets, scopes, user, CancellationToken.None, dataStore).ConfigureAwait(false);
            return user;
            }
        }
    }
}
```

## Usage
Usage is simple. You can either use Dependency Injection or new the client up yourself. You MUST provide the client with your implementation of the `IYouTubeAuthService` for the client to work (see above Prerequisite section).

### Connecting the client
```csharp
// Assuming you have an implementation of the IYouTubeAuthService
var myCoolYouTubeAuthService = new MyCoolYouTubeAuthService();
var client = new YouTubeChatClient(myCoolYouTubeAuthService);
await client.ConnectAsync();
```

### Disconnecting the client
```csharp
client.Disconnect();
```

### Event Registration
The YouTubeChient client currently emits one of three events: `OnConnected`, `OnDisconnected`, and `OnMessageReceived`. You can add event handlers to these events to handle each situation.

```csharp
client.OnConnected += OnYoutubeConnected;
client.OnDisconnected += OnYoutubeDisconnected;
client.OnMessageReceived += OnYoutubeMessageReceivedAsync;

private void OnYoutubeConnected(object sender, bool connected)
{
    this.LogInfo("Youtube chat client connected");
}

private void OnYoutubeDisconnected(object sender, bool connected)
{
    this.LogInfo("Youtube chat client disconnected");
}

private async void OnYoutubeMessageReceivedAsync(object sender, LiveChatMessageListResponse raisedEvent)
{
    // There may be more than one message delivered between polls to the YouTube API
    foreach (var liveChatMessage in raisedEvent.Items)
    {
        this.LogInfo($"Received Youtube message {liveChatMessage.Snippet.DisplayMessage}");
        // Handle the message
    }
}
```
