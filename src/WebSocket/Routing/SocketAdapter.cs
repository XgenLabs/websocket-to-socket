using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace WebSocket.Routing
{
    public class SocketAdapter
    {
        public WsConnection WebSocket { get; }
        public SocketConnection Socket { get; }

        private SocketAdapter(WsConnection webSocket, SocketConnection socket)
        {
            WebSocket = webSocket;
            Socket = socket;
        }

        public static SocketAdapter Create(WsConnection webSocket, SocketConnection socket)
        {
            return new SocketAdapter(webSocket, socket);
        }
    }

    public interface ICommunicationManager
    {
        Task Start(WsConnection webSocket, CancellationTokenSource cancellation);
        void Stop();
    }

    public class ResilientCommunicationManager : ICommunicationManager
    {
        private readonly ICommunicationManager _instance;
        private readonly AsyncRetryPolicy _policy;

        public ResilientCommunicationManager(ILogger<ResilientCommunicationManager> logger, ServiceFactory serviceFactory)
        {
            _policy = Policy.Handle<Exception>()
                    .Or<SocketException>()
                    .WaitAndRetryAsync(
                        // number of retries
                        3,
                        // exponential backofff
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                        // on retry
                        (exception, timeSpan, retryCount, context) =>
                        {
                            var msg = $"Retry {retryCount} implemented with Polly's " +
                                      $"of {context.PolicyKey} " +
                                      $"at {context.OperationKey}, " +
                                      $"due to: {exception}.";

                            logger.LogError(exception, msg);
                        });

            _instance = serviceFactory.GetInstance<ComunicationManager>();
        }

        public Task Start(WsConnection webSocket, CancellationTokenSource cancellation)
        {
            return _policy.ExecuteAsync(() => _instance.Start(webSocket, cancellation));
        }

        public void Stop()
        {
            _instance.Stop();
        }
    }

    public class ComunicationManager : ICommunicationManager
    {
        private IPEndPoint EndPoint => new IPEndPoint(IPAddress.Parse("127.0.0.1"), 11000);

        private readonly ILogger<ComunicationManager> _logger;
        private readonly TcpConnector _connector;
        private CancellationTokenSource _cancellation;

        public ComunicationManager(ILogger<ComunicationManager> logger)
        {
            _logger = logger;
            _connector = new TcpConnector();
        }

        public async Task Start(WsConnection webSocket, CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;

            using var connection = await _connector.ConnectAsync(EndPoint);

            var token = _cancellation.Token;
            var adapter = SocketAdapter.Create(webSocket, connection);
            var receiveLoop = ReceiveLoop(adapter, token);
            var handshakeLoop = KeepALiveLoop(adapter, token);

            // Create class to loop over taks until cancel or exception
            await new TaskRunner(new[] { receiveLoop, handshakeLoop }, token).Start();
        }

        public void Stop()
        {
            _cancellation.Cancel();
        }

        private async Task ReceiveLoop(SocketAdapter adapter, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var content = await adapter.Socket.ReceiveAsync(512, TimeSpan.FromSeconds(15), token);
                    await adapter.WebSocket.SendAsync("Update", content, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (SocketException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Something went wrong while receiving a message");
                }
            }
        }

        private async Task KeepALiveLoop(SocketAdapter adapter, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await adapter.Socket.SendAsync($"ping {DateTime.UtcNow.ToString("o")}", token);
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        }
    }

    public class WsConnection
    {
        private readonly IClientProxy _socket;

        public WsConnection(IClientProxy socket)
        {
            _socket = socket;
        }

        public Task SendAsync(string method, object arg, CancellationToken token)
        {
            return _socket.SendAsync(method, arg, token);
        }
    }

    public class SocketConnection : IDisposable
    {
        private readonly Socket _socket;
        private readonly Encoding DefaultEncoding = Encoding.GetEncoding("iso-8859-1");

        public SocketConnection(Socket socket)
        {
            _socket = socket;
        }

        public async Task SendAsync(string message, CancellationToken token)
        {
            var buffer = DefaultEncoding.GetBytes(message);
            await _socket.SendAsync(buffer, SocketFlags.None, token);
        }

        public async Task<string> ReceiveAsync(int messageSize, TimeSpan socketTimeout, CancellationToken token)
        {
            // Check if is necessary to read data in loop
            var buffer = new byte[messageSize];

            var timeout = Task.Delay(socketTimeout);
            var receive = _socket.ReceiveAsync(buffer, SocketFlags.None, token).AsTask();
            var finishedTask = await Task.WhenAny(receive, timeout);

            if (timeout == finishedTask)
            {
                throw new SocketException((int)SocketError.TimedOut);
            }

            var read = await receive;

            if (read == 0)
            {
                // Force shutdown when is not to read
                throw new SocketException((int)SocketError.Shutdown);
            }
            return DefaultEncoding.GetString(buffer, 0, read);
        }

        #region IDisposable Support
        private bool disposedValue = false;

        void Dispose(bool disposing)
        {
            if (disposedValue)
            {
                return;
            }
            if (disposing)
            {
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
                catch { }

                try
                {
                    _socket.Close();
                }
                catch { }
                _socket.Dispose();
            }

            disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

    public sealed class TcpConnector
    {
        public async Task<SocketConnection> ConnectAsync(IPEndPoint endpoint)
        {
            var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(endpoint);

            return new SocketConnection(socket);
        }
    }

    public static class Constants
    {
        public static class Encoding
        {
            public static readonly System.Text.Encoding ISO_8859_1 = System.Text.Encoding.GetEncoding("iso-8859-1");
        }
    }

    public class TaskRunner
    {
        private readonly Task[] _tasks;
        private readonly CancellationToken _token;


        public TaskRunner(Task[] tasks, CancellationToken token)
        {
            _tasks = tasks;
            _token = token;
        }

        public async Task Start()
        {
            Exception exception;
            while (false == _token.IsCancellationRequested)
            {
                await Task.WhenAny(_tasks);

                foreach (var task in _tasks)
                {
                    if (task.Exception != null)
                    {
                        exception = task.Exception;
                        break;
                    }
                }
            }

            var isCompleted = _tasks.All(task => task.IsCompleted);

            if (false == isCompleted) // At least one task is incomplete
            {
                await Task.Delay(15 * 1_000); // wait for 15s before force 
            }

            foreach (var task in _tasks)
            {
                try
                {
                    task.Dispose();
                }
                catch
                {
                }
            }
        }
    }

    public delegate object ServiceFactory(Type serviceType);

    public static class ServiceFactoryExtensions
    {
        public static T GetInstance<T>(this ServiceFactory factory)
            => (T)factory(typeof(T));

        public static IEnumerable<T> GetInstances<T>(this ServiceFactory factory)
            => (IEnumerable<T>)factory(typeof(IEnumerable<T>));
    }
}