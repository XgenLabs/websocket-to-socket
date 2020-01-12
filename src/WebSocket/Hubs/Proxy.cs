using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace WebSocket.Hubs
{
    public class Proxy : Hub
    {
        private readonly ILogger<Proxy> _logger;

        private HttpRequest Request => Context.GetHttpContext().Request;

        public Proxy(ILogger<Proxy> logger)
        {
            _logger = logger;
        }

        public override Task OnDisconnectedAsync(System.Exception exception)
        {
            return base.OnDisconnectedAsync(exception);
        }

        public override Task OnConnectedAsync()
        {
            var username = Request.Query["username"];
            _logger.LogDebug($"User: {username} connected");
            return base.OnConnectedAsync();
        }
    }
}