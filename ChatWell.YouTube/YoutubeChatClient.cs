using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace ChatWell.YouTube
{
    public interface IYouTubeChatClient
    {
        bool IsConnected { get; }

        bool IsInitialized { get; }

        Task ConnectAsync();

        void Disconnect();

        Task<LiveChatMessage> SendMessageAsync(string message);

        event EventHandler<bool> OnConnected;

        event EventHandler<bool> OnDisconnected;

        event EventHandler<LiveChatMessageListResponse> OnMessageReceived;
    }

    public class YouTubeChatClient : IYouTubeChatClient
    {
        private readonly IYouTubeAuthService youtubeAuthService;
        private Thread chatThread;
        private bool disconnect = false;
        private string liveChatId;
        private YouTubeService youtubeService;

        public bool IsConnected { get; private set; }

        public bool IsInitialized { get; private set; }

        public YouTubeChatClient(IYouTubeAuthService youtubeAuthService)
        {
            this.youtubeAuthService = youtubeAuthService;
        }

        public async Task ConnectAsync()
        {
            if (!this.IsInitialized)
            {
                await this.InitializeAsync().ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(this.liveChatId))
            {
                this.chatThread = new Thread(async () =>
                {
                    var isFirstRun = true;
                    var nextPageToken = string.Empty;
                    var pollingIntervalMilliseconds = 0L;
                    var retryAttempts = 0;

                    while (!this.disconnect)
                    {
                        try
                        {
                            var response = await GetLiveChatMessagesAsync(this.liveChatId, nextPageToken).ConfigureAwait(false);
                            if (response != null)
                            {
                                nextPageToken = response.NextPageToken;
                                pollingIntervalMilliseconds = response.PollingIntervalMillis ?? 1000;

                                if (!isFirstRun)
                                {
                                    this.OnMessageReceived?.Invoke(this, response);
                                }

                                retryAttempts = 0;
                                isFirstRun = false;
                                await Task.Delay((int)pollingIntervalMilliseconds).ConfigureAwait(false);
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            if (retryAttempts == 5)
                            {
                                throw;
                            }

                            retryAttempts++;
                            await Task.Delay(retryAttempts * 1000).ConfigureAwait(false);
                        }

                        await Task.Delay((int)pollingIntervalMilliseconds).ConfigureAwait(false);
                    }
                });

                this.chatThread.Start();
                this.IsConnected = true;
                this.OnConnected?.Invoke(this, true);
            }
        }

        public void Disconnect()
        {
            this.disconnect = true;
            while (this.chatThread.IsAlive)
            {
                Thread.Sleep(100);
            }

            this.IsConnected = false;
            this.OnDisconnected?.Invoke(this, true);
        }

        public async Task<LiveChatMessage> SendMessageAsync(string message)
        {
            if (!string.IsNullOrWhiteSpace(this.liveChatId))
            {
                var liveChatMessage = new LiveChatMessage
                {
                    Snippet = new LiveChatMessageSnippet
                    {
                        LiveChatId = this.liveChatId,
                        Type = "textMessageEvent",
                        TextMessageDetails = new LiveChatTextMessageDetails { MessageText = message }
                    }
                };

                var request = this.youtubeService.LiveChatMessages.Insert(liveChatMessage, "snippet");
                return await request.ExecuteAsync().ConfigureAwait(false);
            }

            return null;
        }

        private async Task<string> GetLiveChatIdAsync()
        {
            var request = this.youtubeService.LiveBroadcasts.List("snippet");
            request.Fields = "items/snippet/liveChatId";
            request.BroadcastType = LiveBroadcastsResource.ListRequest.BroadcastTypeEnum.All;
            request.BroadcastStatus = LiveBroadcastsResource.ListRequest.BroadcastStatusEnum.All;

            var response = await request.ExecuteAsync().ConfigureAwait(false);
            return response?.Items?.FirstOrDefault()?.Snippet?.LiveChatId;
        }

        private Task<LiveChatMessageListResponse> GetLiveChatMessagesAsync(string liveChatId, string nextPageToken)
        {
            var request = this.youtubeService.LiveChatMessages.List(liveChatId, "snippet,authorDetails");
            request.PageToken = string.IsNullOrWhiteSpace(nextPageToken) ? "" : nextPageToken;

            return request.ExecuteAsync();
        }

        private async Task InitializeAsync()
        {
            var userCredential = await youtubeAuthService.GetUserCredentialAsync().ConfigureAwait(false);
            var initializer = new BaseClientService.Initializer
            {
                HttpClientInitializer = userCredential,
                ApplicationName = this.GetType().ToString()
            };

            this.youtubeService = new YouTubeService(initializer);
            this.liveChatId = await this.GetLiveChatIdAsync().ConfigureAwait(false);
            this.IsInitialized = true;
        }

        public event EventHandler<bool> OnConnected;

        public event EventHandler<bool> OnDisconnected;

        public event EventHandler<LiveChatMessageListResponse> OnMessageReceived;
    }
}
