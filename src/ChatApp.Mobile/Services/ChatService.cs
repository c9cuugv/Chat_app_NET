using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Tasks;

namespace ChatApp.Mobile.Services
{
    public class ChatService
    {
        private HubConnection _hubConnection;
        private readonly IAuthService _authService;

        public ChatService(IAuthService authService)
        {
            _authService = authService;
        }

        // Expose for testing to verify the instance changes
        public HubConnection HubConnection => _hubConnection;

        public async Task ConnectAsync()
        {
            if (_hubConnection?.State == HubConnectionState.Connected)
                return;

            var token = await _authService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return;

            var baseUrl = AuthService.BaseUrl;

            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{baseUrl}/chatHub", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(token);
                })
                .Build();

            await _hubConnection.StartAsync();
        }
    }
}
