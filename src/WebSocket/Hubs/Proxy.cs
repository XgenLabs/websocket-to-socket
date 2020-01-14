using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using WebSocket.Routing;

namespace WebSocket.Hubs
{
    public class Proxy : Hub
    {
        readonly ConcurrentDictionary<string, ICommunicationManager> _connections = new ConcurrentDictionary<string, ICommunicationManager>(Environment.ProcessorCount * 2, 101);

        private readonly ILogger<Proxy> _logger;
        private readonly ServiceFactory _serviceFactory;

        private HttpRequest Request => Context.GetHttpContext().Request;

        public Proxy(ILogger<Proxy> logger, ServiceFactory serviceFactory)
        {
            _logger = logger;
            _serviceFactory = serviceFactory;
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out var communication))
            {
                communication.Stop();
            }

            return base.OnDisconnectedAsync(exception);
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();

            try
            {
                var cancellation = new CancellationTokenSource();
                var wsConnection = new WsConnection(Clients.Client(Context.ConnectionId));
                var comunicationManager = _serviceFactory.GetInstance<ResilientCommunicationManager>();
                _connections.TryAdd(Context.ConnectionId, comunicationManager);

                await comunicationManager.Start(wsConnection, cancellation);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "A fatal error has occurred.");
            }

            _logger.LogInformation("Execution ended.");
        }
    }
}